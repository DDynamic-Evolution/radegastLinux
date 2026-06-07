package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"log"
	"net"
	"os"
	"os/signal"
	"strings"
	"syscall"
	"time"

	"github.com/vishvananda/netlink"
	"golang.zx2c4.com/wireguard/wgctrl"
	"golang.zx2c4.com/wireguard/wgctrl/wgtypes"
)

const linkPrefix = "rgv-"

type Request struct {
	ID     int           `json:"id,omitempty"`
	Cmd    string        `json:"cmd"`
	Name   string        `json:"name"`
	Config *TunnelConfig `json:"config,omitempty"`
}

type Response struct {
	ID      int           `json:"id,omitempty"`
	Ok      bool          `json:"ok"`
	Error   string        `json:"error,omitempty"`
	Status  *TunnelStatus `json:"status,omitempty"`
	Tunnels []string      `json:"tunnels,omitempty"`
}

type TunnelConfig struct {
	PrivateKey string       `json:"private_key"`
	Address    string       `json:"address"`
	Peers      []PeerConfig `json:"peers"`
}

type PeerConfig struct {
	PublicKey           string   `json:"public_key"`
	Endpoint            string   `json:"endpoint"`
	AllowedIPs          []string `json:"allowed_ips"`
	PersistentKeepalive int      `json:"persistent_keepalive"`
}

type TunnelStatus struct {
	Up      bool   `json:"up"`
	Address string `json:"address,omitempty"`
	BytesRx int64  `json:"bytes_rx,omitempty"`
	BytesTx int64  `json:"bytes_tx,omitempty"`
}

var wgClient *wgctrl.Client

func main() {
	log.SetFlags(0)
	log.SetPrefix("vpn-helper: ")

	var err error
	wgClient, err = wgctrl.New()
	if err != nil {
		log.Fatalf("failed to create wgctrl client: %v", err)
	}
	defer wgClient.Close()

	sc := make(chan os.Signal, 1)
	signal.Notify(sc, syscall.SIGINT, syscall.SIGTERM)
	go func() {
		<-sc
		cleanupAll()
		os.Exit(0)
	}()

	scanner := bufio.NewScanner(os.Stdin)
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}

		var req Request
		if err := json.Unmarshal([]byte(line), &req); err != nil {
			writeErr(0, fmt.Sprintf("invalid JSON: %v", err))
			continue
		}

		handle(&req)
	}
}

func handle(req *Request) {
	switch req.Cmd {
	case "up":
		handleUp(req)
	case "down":
		handleDown(req)
	case "status":
		handleStatus(req)
	case "list":
		handleList(req)
	case "shutdown":
		cleanupAll()
		os.Exit(0)
	default:
		writeErr(req.ID, fmt.Sprintf("unknown command: %s", req.Cmd))
	}
}

func handleUp(req *Request) {
	if req.Name == "" {
		writeErr(req.ID, "name is required")
		return
	}
	if req.Config == nil {
		writeErr(req.ID, "config is required")
		return
	}

	ifName := linkPrefix + req.Name

	privKey, err := wgtypes.ParseKey(req.Config.PrivateKey)
	if err != nil {
		writeErr(req.ID, fmt.Sprintf("invalid private key: %v", err))
		return
	}

	link := &netlink.Wireguard{
		LinkAttrs: netlink.LinkAttrs{Name: ifName},
	}
	if err := netlink.LinkAdd(link); err != nil {
		writeErr(req.ID, fmt.Sprintf("failed to create link: %v", err))
		return
	}

	cfg := wgtypes.Config{
		PrivateKey: &privKey,
	}

	for _, p := range req.Config.Peers {
		pubKey, err := wgtypes.ParseKey(p.PublicKey)
		if err != nil {
			netlink.LinkDel(link)
			writeErr(req.ID, fmt.Sprintf("invalid peer public key: %v", err))
			return
		}

		var endpoint *net.UDPAddr
		if p.Endpoint != "" {
			addr, err := net.ResolveUDPAddr("udp", p.Endpoint)
			if err != nil {
				netlink.LinkDel(link)
				writeErr(req.ID, fmt.Sprintf("invalid endpoint %q: %v", p.Endpoint, err))
				return
			}
			endpoint = addr
		}

		var allowedIPs []net.IPNet
		for _, a := range p.AllowedIPs {
			_, cidr, err := net.ParseCIDR(a)
			if err != nil {
				netlink.LinkDel(link)
				writeErr(req.ID, fmt.Sprintf("invalid allowed-ip %q: %v", a, err))
				return
			}
			allowedIPs = append(allowedIPs, *cidr)
		}

		peerCfg := wgtypes.PeerConfig{
			PublicKey:                   pubKey,
			Endpoint:                    endpoint,
			AllowedIPs:                  allowedIPs,
			PersistentKeepaliveInterval: durationPtr(p.PersistentKeepalive),
			ReplaceAllowedIPs:           true,
		}
		cfg.Peers = append(cfg.Peers, peerCfg)
	}

	if err := wgClient.ConfigureDevice(ifName, cfg); err != nil {
		netlink.LinkDel(link)
		writeErr(req.ID, fmt.Sprintf("failed to configure wg device: %v", err))
		return
	}

	_, ipnet, err := net.ParseCIDR(req.Config.Address)
	if err != nil {
		netlink.LinkDel(link)
		writeErr(req.ID, fmt.Sprintf("invalid address %q: %v", req.Config.Address, err))
		return
	}
	nlAddr := &netlink.Addr{IPNet: ipnet}
	if err := netlink.AddrAdd(link, nlAddr); err != nil {
		netlink.LinkDel(link)
		writeErr(req.ID, fmt.Sprintf("failed to add address: %v", err))
		return
	}

	if err := netlink.LinkSetUp(link); err != nil {
		netlink.LinkDel(link)
		writeErr(req.ID, fmt.Sprintf("failed to set link up: %v", err))
		return
	}

	for _, peer := range req.Config.Peers {
		for _, a := range peer.AllowedIPs {
			_, cidr, err := net.ParseCIDR(a)
			if err != nil {
				continue
			}
			if isDefault(cidr) {
				continue
			}
			route := &netlink.Route{
				Dst:       cidr,
				LinkIndex: link.Attrs().Index,
			}
			_ = netlink.RouteAdd(route)
		}
	}

	writeResp(req.ID, Response{Ok: true})
}

func handleDown(req *Request) {
	ifName := linkPrefix + req.Name
	link, err := netlink.LinkByName(ifName)
	if err != nil {
		writeErr(req.ID, fmt.Sprintf("interface %s not found", ifName))
		return
	}
	if err := netlink.LinkDel(link); err != nil {
		writeErr(req.ID, fmt.Sprintf("failed to delete link: %v", err))
		return
	}
	writeResp(req.ID, Response{Ok: true})
}

func handleStatus(req *Request) {
	ifName := linkPrefix + req.Name

	link, err := netlink.LinkByName(ifName)
	if err != nil {
		writeResp(req.ID, Response{Ok: true, Status: &TunnelStatus{Up: false}})
		return
	}

	dev, err := wgClient.Device(ifName)
	if err != nil {
		writeResp(req.ID, Response{Ok: true, Status: &TunnelStatus{Up: false}})
		return
	}

	var rx, tx int64
	for _, p := range dev.Peers {
		rx += p.ReceiveBytes
		tx += p.TransmitBytes
	}

	addr := ""
	addrs, _ := netlink.AddrList(link, netlink.FAMILY_V4)
	if len(addrs) > 0 {
		addr = addrs[0].IPNet.String()
	}

	writeResp(req.ID, Response{
		Ok: true,
		Status: &TunnelStatus{
			Up:      link.Attrs().OperState == netlink.OperUp,
			Address: addr,
			BytesRx: rx,
			BytesTx: tx,
		},
	})
}

func handleList(req *Request) {
	links, err := netlink.LinkList()
	if err != nil {
		writeErr(req.ID, fmt.Sprintf("failed to list links: %v", err))
		return
	}

	var tunnels []string
	for _, l := range links {
		if strings.HasPrefix(l.Attrs().Name, linkPrefix) {
			tunnels = append(tunnels, strings.TrimPrefix(l.Attrs().Name, linkPrefix))
		}
	}

	writeResp(req.ID, Response{Ok: true, Tunnels: tunnels})
}

func cleanupAll() {
	links, err := netlink.LinkList()
	if err != nil {
		return
	}
	for _, l := range links {
		if strings.HasPrefix(l.Attrs().Name, linkPrefix) {
			netlink.LinkDel(l)
		}
	}
}

func isDefault(cidr *net.IPNet) bool {
	ones, bits := cidr.Mask.Size()
	return ones == 0 && bits > 0
}

func durationPtr(seconds int) *time.Duration {
	if seconds <= 0 {
		return nil
	}
	d := time.Duration(seconds) * time.Second
	return &d
}

func writeResp(id int, resp Response) {
	resp.ID = id
	b, _ := json.Marshal(resp)
	fmt.Println(string(b))
}

func writeErr(id int, msg string) {
	writeResp(id, Response{Ok: false, Error: msg})
}
