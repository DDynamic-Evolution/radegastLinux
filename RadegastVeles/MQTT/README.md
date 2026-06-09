# MQTT Integration – Radegast Veles

## Configuration

All settings are configured in the Preferences dialog under the **MQTT** tab and persisted in `settings.xml`.

| Field | Default | Description |
|---|---|---|
| `Host` | `localhost` | MQTT broker hostname/IP |
| `Port` | `1883` | MQTT broker port |
| `Username` | (empty) | Authentication (optional) |
| `Password` | (empty) | Authentication (optional) |
| `Root Topic` | `radegast` | Topic prefix for all messages |
| `QoS` | `1` | Quality of Service (0, 1, 2) |
| `Use TLS` | `false` | Enable TLS encryption |
| `Publish outgoing chat` | `true` | Publish chat send events |
| `Publish incoming chat` | `true` | Publish chat receive events |
| `Publish outgoing IMs` | `true` | Publish IM send events |
| `Publish incoming IMs` | `true` | Publish IM receive events |
| `Subscribe to commands` | `true` | Listen for remote control commands |

---

## Topics

All topics are relative to the configured `Root Topic`.  
Example with `Root Topic = "radegast"`:

### Published Topics (Client → Broker)

#### Chat Sent
**Topic:** `{root}/chat/send`  
**Payload:**
```json
{"message":"Hello world!","type":"Normal","channel":0}
```

| Field | Type | Description |
|---|---|---|
| `message` | string | The sent chat text |
| `type` | string | `Normal`, `Whisper` or `Shout` |
| `channel` | int | Chat channel (`0` = public) |

**Enabled by:** `Publish outgoing chat`

#### Chat Received
**Topic:** `{root}/chat/receive`  
**Payload:**
```json
{"from":"Avatar Name","message":"Hi!","type":"Normal","source":"Agent"}
```

| Field | Type | Description |
|---|---|---|
| `from` | string | Sender name |
| `message` | string | Received chat text |
| `type` | string | Chat type |
| `source` | string | Source (`Agent`, `Object`, `System`) |

**Enabled by:** `Publish incoming chat`

#### IM Sent
**Topic:** `{root}/im/send`  
**Payload:**
```json
{"to":"Avatar Name","type":"personal","message":"Hello via IM!"}
```

| Field | Type | Description |
|---|---|---|
| `to` | string | Recipient name or group name |
| `type` | string | `personal`, `group` or `conference` |
| `message` | string | Sent IM text |

**Enabled by:** `Publish outgoing IMs`

#### IM Received
**Topic:** `{root}/im/receive`  
**Payload:**
```json
{"from":"Avatar Name","type":"personal","message":"Reply via IM!"}
```

| Field | Type | Description |
|---|---|---|
| `from` | string | Sender (for group: `Sender (GroupName)`) |
| `type` | string | `personal`, `group` or `conference` |
| `message` | string | Received IM text |

**Enabled by:** `Publish incoming IMs`

---

### Subscribed Topics (Broker → Client)

#### Send Chat (Command)
**Topic:** `{root}/cmd/chat`  
**Payload:** Any text (sent as normal chat on channel 0)

Example:  
Publish to `radegast/cmd/chat` with payload `"Hello from MQTT!"`  
→ The client sends `"Hello from MQTT!"` as normal chat to the current region.

**Enabled by:** `Subscribe to commands`

#### Teleport (Command)
**Topic:** `{root}/cmd/teleport`  
**Payload (JSON):**
```json
{"region":"Region Name","x":128,"y":128,"z":25}
```

| Field | Type | Description |
|---|---|---|
| `region` | string | Destination region name (optional; defaults to current region) |
| `x`, `y`, `z` | number | Destination coordinates (optional; default: 128, 128, 25) |

If the payload is a plain string instead of JSON, it is treated as the region name.

Example:  
Publish to `radegast/cmd/teleport` with payload `"{\"region\":\"Ahern\",\"x\":100,\"y\":100,\"z\":30}"`  
→ The client teleports to Ahern (100, 100, 30).

**Enabled by:** `Subscribe to commands`

#### List Avatars (Command)
**Topic:** `{root}/cmd/avatars`  
**Payload:** Ignored (any value)

Triggers a publish of all avatars on the current region to:  
**Topic:** `{root}/location/avatars`  
**Payload:**
```json
[
  {"id":"a1b2c3d4-...","name":"Avatar One","x":100.0,"y":200.0,"z":30.0},
  {"id":"e5f6g7h8-...","name":"Avatar Two","x":150.0,"y":250.0,"z":35.0}
]
```

| Field | Type | Description |
|---|---|---|
| `id` | string | Avatar UUID |
| `name` | string | Avatar display name |
| `x`, `y`, `z` | number | Avatar global position (rounded to 1 decimal) |

Also published automatically on teleport finish when `Publish location` is enabled.

**Enabled by:** `Subscribe to commands`
