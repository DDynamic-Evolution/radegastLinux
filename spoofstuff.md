// Hardware ID spoofing for Second Life login.
// Intercepts three identifiers SL uses to fingerprint/ban accounts:
//   id0  = MD5(disk serial)   sent as login param
//   mac  = MD5(MAC address)   sent as login param
//   raw node_id / machine_id  sent in viewer stats
// All replaced with deterministic fakes from: MD5(seed + username)
// Seed persists in settings so same fake IDs return each session.

// ─── hwspoof_engine.h ────────────────────────────────────────────────────────

const std::string& hwspoof_get_seed();
const std::string& hwspoof_get_username();

void hwspoof_reroll_seed();
void hwspoof_set_seed(const std::string&);
void hwspoof_set_username(const std::string& username);

void hwspoof_set_real_serial(std::string serial);
void hwspoof_set_real_nodeid(unsigned char nodeid[6]);
void hwspoof_set_real_machineid(unsigned char machineid[6]);

const std::string& hwspoof_get_real_serial();
const std::string& hwspoof_get_real_macid_str();
const std::string& hwspoof_get_real_nodeid_str();
const std::string& hwspoof_get_real_machineid_str();

const std::string& hwspoof_get_id0();    // fake id0 sent on login
const std::string& hwspoof_get_macid();  // fake mac sent on login

void hwspoof_get_faux_nodeid(unsigned char out[6]);
const std::string& hwspoof_get_faux_nodeid_str();
void hwspoof_get_faux_machineid(unsigned char out[6]);
const std::string& hwspoof_get_faux_machineid_str();

S32 sys_getNodeID_original(unsigned char* node_id);
S32 sys_getMachineID_original(unsigned char* unique_id, size_t len);

void hwspoof_fake_support_info(LLSD& info, std::string build_type_string = std::string());

// ─── hwspoof_engine.cpp ──────────────────────────────────────────────────────

static std::string lo_seed;
static std::string lo_username;

static std::string real_serial;
static std::string real_macid_str;
static unsigned char real_nodeid[6];
static unsigned char real_machineid[6];
static std::string real_nodeid_str;
static std::string real_machineid_str;

static std::string spoofed_id0;
static std::string spoofed_macid;
static unsigned char faux_nodeid[6];
static unsigned char faux_machineid[6];
static std::string faux_nodeid_str;
static std::string faux_machineid_str;

static std::string format_mac(unsigned char mac[6])
{
    return llformat("%02x-%02x-%02x-%02x-%02x-%02x",
                    mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
}

// Mirrors llHashedUniqueID: MD5(machineid) preferred, fallback MD5(nodeid), else zeroes
static std::string simulate_macid(unsigned char nodeid[6], unsigned char machineid[6])
{
    U32 sum_nodeid = 0, sum_machineid = 0;
    for (int i = 0; i < 6; ++i) sum_nodeid    += nodeid[i];
    for (int i = 0; i < 6; ++i) sum_machineid += machineid[i];

    unsigned char* input = 0;
    if (sum_machineid != 0)      input = machineid;
    else if (sum_nodeid != 0)    input = nodeid;

    char digest[33];
    if (input)
    {
        LLMD5 hash;
        hash.update(input, 6);
        hash.finalize();
        hash.hex_digest(&digest[0]);
    }
    else
    {
        memcpy(&digest[0], "00000000000000000000000000000000", 33);
    }
    return digest;
}

static void regen_seed()
{
    LLMD5 seedgen;
    for (int i = 0; i < 4; ++i)
    {
        S32 r = ll_rand();
        seedgen.update((unsigned char*)&r, sizeof(r));
    }
    seedgen.update((unsigned char*)real_serial.data(), real_serial.size());
    seedgen.update(real_nodeid, sizeof(real_nodeid));
    seedgen.update(real_machineid, sizeof(real_machineid));
    seedgen.finalize();

    lo_seed.resize(33);
    seedgen.hex_digest((char*)&lo_seed[0]);
    lo_seed.resize(16);
}

const std::string& hwspoof_get_seed()
{
    if (lo_seed.empty()) regen_seed();
    return lo_seed;
}

const std::string& hwspoof_get_username() { return lo_username; }

static void regen_spoofed_ids()
{
    const std::string& seed     = hwspoof_get_seed();
    const std::string& username = hwspoof_get_username();

    // fake id0 = MD5("id0" + seed + username)
    {
        LLMD5 idgen;
        idgen.update((unsigned char*)"id0", 3);
        idgen.update((unsigned char*)seed.data(), seed.size());
        idgen.update((unsigned char*)username.data(), username.size());
        idgen.finalize();
        spoofed_id0.resize(33);
        idgen.hex_digest((char*)&spoofed_id0[0]);
        spoofed_id0.resize(32);
    }

    // fake nodeid + machineid = MD5("fauxids" + seed + username)
    // Keeps real OUI (first 3 MAC bytes) so address looks legitimate
    {
        LLMD5 idgen;
        unsigned char digest[16];
        idgen.update((unsigned char*)"fauxids", 7);
        idgen.update((unsigned char*)seed.data(), seed.size());
        idgen.update((unsigned char*)username.data(), username.size());
        idgen.finalize();
        idgen.raw_digest(digest);

        int i = 0;
        if ((real_nodeid[0] + real_nodeid[1] + real_nodeid[2]) != 0)
        {
            for (; i < 3; ++i) faux_nodeid[i] = real_nodeid[i]; // copy real OUI
        }
        else
        {
            faux_nodeid[0] = 0x00;
            faux_nodeid[1] = digest[1] & 0x1F;
            i = 2;
        }
        for (; i < 6; ++i) faux_nodeid[i] = digest[i];
        faux_nodeid_str = format_mac(faux_nodeid);

        for (int j = 0; j < 6; ++j) faux_machineid[j] = digest[6 + j];
        faux_machineid_str = format_mac(faux_machineid);
    }

    spoofed_macid = simulate_macid(faux_nodeid, faux_machineid);
}

void hwspoof_reroll_seed()          { regen_seed(); regen_spoofed_ids(); }
void hwspoof_set_seed(const std::string& seed) { lo_seed = seed; regen_spoofed_ids(); }
void hwspoof_set_username(const std::string& username)
    { lo_username = utf8str_tolower(username); regen_spoofed_ids(); }

void hwspoof_set_real_serial(std::string serial) { real_serial = serial; }

void hwspoof_set_real_nodeid(unsigned char nodeid[6])
{
    for (int i = 0; i < 6; ++i) real_nodeid[i] = nodeid[i];
    real_nodeid_str = format_mac(real_nodeid);
    real_macid_str  = simulate_macid(real_nodeid, real_machineid);
}

void hwspoof_set_real_machineid(unsigned char machineid[6])
{
    for (int i = 0; i < 6; ++i) real_machineid[i] = machineid[i];
    real_machineid_str = format_mac(real_machineid);
    real_macid_str     = simulate_macid(real_nodeid, real_machineid);
}

const std::string& hwspoof_get_real_serial()        { return real_serial; }
const std::string& hwspoof_get_real_nodeid_str()     { return real_nodeid_str; }
const std::string& hwspoof_get_real_machineid_str()  { return real_machineid_str; }
const std::string& hwspoof_get_real_macid_str()      { return real_macid_str; }

const std::string& hwspoof_get_id0()
{
    const std::string& c = hwspoof_get_custom_id0();
    return c.empty() ? spoofed_id0 : c;
}

const std::string& hwspoof_get_macid()
{
    const std::string& c = hwspoof_get_custom_macid();
    return c.empty() ? spoofed_macid : c;
}

void hwspoof_get_faux_nodeid(unsigned char out[6])
    { for (int i = 0; i < 6; ++i) out[i] = faux_nodeid[i]; }
const std::string& hwspoof_get_faux_nodeid_str()    { return faux_nodeid_str; }

void hwspoof_get_faux_machineid(unsigned char out[6])
    { for (int i = 0; i < 6; ++i) out[i] = faux_machineid[i]; }
const std::string& hwspoof_get_faux_machineid_str() { return faux_machineid_str; }

// Reports as stock Firestorm 7.2.3.80036 to hide the fork
void hwspoof_fake_support_info(LLSD& info, std::string build_type_string)
{
#if LL_WINDOWS
    info["BUILD_DATE"] = "Feb  9 2026"; info["BUILD_TIME"] = "18:30:30";
    info["COMPILER"] = "MSVC";          info["COMPILER_VERSION"] = "1944";
#else
    info["BUILD_DATE"] = "Feb  9 2026"; info["BUILD_TIME"] = "20:01:31";
    info["COMPILER"] = "GCC";           info["COMPILER_VERSION"] = "130100";
#endif
    info["J2C_VERSION"]          = "KDU v8.6.1";
    info["AUDIO_DRIVER_VERSION"] = "FMOD Studio 2.03.07";
    info["SIMD"]       = "AVX2";
    info["BUILD_TYPE"] = build_type_string;
}

// ─── hwspoof_extras.h ────────────────────────────────────────────────────────

#define FEAT_CONVENIENCE         0x00000001U
#define FEAT_BYPASS_EXPORT_PERMS 0x00000002U
#define FEAT_ENHANCED_EXPORT     0x00000004U
#define FEAT_ANONYMIZE_EXPORTS   0x00000008U
#define FEAT_MD5_LOGINS          0x00000010U  // paste raw MD5 hash as password
#define FEAT_MASK                0x0000001FU

void hwspoof_set_flags(unsigned flags, unsigned mask);
unsigned hwspoof_get_flags();
unsigned hwspoof_get_mask();
unsigned hwspoof_new_defaulted_flags();
void hwspoof_enable_flag(unsigned flag);
void hwspoof_disable_flag(unsigned flag);
bool hwspoof_check_flag(unsigned flag);

void hwspoof_set_custom_ids(const std::string& username, const std::string& id0, const std::string& macid);
void hwspoof_set_custom_id0(const std::string& id0);
void hwspoof_set_custom_macid(const std::string& macid);
const std::string& hwspoof_get_custom_username();
const std::string& hwspoof_get_custom_id0();
const std::string& hwspoof_get_custom_macid();

// ─── hwspoof_extras.cpp ──────────────────────────────────────────────────────

static std::string custom_username;
static std::string custom_id0;
static std::string custom_macid;

void hwspoof_set_custom_ids(const std::string& username, const std::string& id0, const std::string& macid)
    { custom_username = username; custom_id0 = id0; custom_macid = macid; }

void hwspoof_set_custom_id0(const std::string& id0)    { custom_id0   = id0; }
void hwspoof_set_custom_macid(const std::string& macid) { custom_macid = macid; }

const std::string& hwspoof_get_custom_username() { return custom_username; }
const std::string& hwspoof_get_custom_id0()      { return custom_id0; }
const std::string& hwspoof_get_custom_macid()    { return custom_macid; }

// ─── lluuid.cpp patch — getNodeID() returns fake MAC bytes ───────────────────

S32 LLUUID::getNodeID(unsigned char* node_id)
{
    hwspoof_get_faux_nodeid(node_id);
    return 1;
}

// ─── llmachineid.cpp patch — getUniqueID() returns fake machine ID ───────────

S32 LLMachineID::getUniqueID(unsigned char* unique_id, size_t len)
{
    unsigned char buf[6];
    hwspoof_get_faux_machineid(buf);
    for (size_t i = 0; i < std::min<size_t>(len, 6); ++i)
        unique_id[i] = buf[i];
    return 1;
}

// ─── llappviewer.cpp — startup: load seed, capture real IDs ─────────────────

    std::string spoof_seed = gSavedSettings.getString("HWSpoofSeed");

    if (spoof_seed.empty())
    {
        hwspoof_reroll_seed();
        gSavedSettings.setString("HWSpoofSeed", hwspoof_get_seed());
    }
    else
    {
        hwspoof_set_seed(spoof_seed);
    }

    hwspoof_set_real_serial(generateSerialNumber());

    {
        unsigned char node_id[6] = {};
        if (sys_getNodeID_original(node_id))
            hwspoof_set_real_nodeid(node_id);
    }

    {
        unsigned char machine_id[6] = {};
        if (sys_getMachineID_original(machine_id, 6))
            hwspoof_set_real_machineid(machine_id);
    }

    mSerialNumber = hwspoof_get_id0();

const std::string& LLAppViewer::getSerialNumber()
{
    return hwspoof_get_id0();
}

    if (!unfaked_string)
        hwspoof_fake_support_info(info, ...);

// ─── fspanellogin.cpp — login panel wires username + saves custom IDs ────────

    hwspoof_set_username(canonicalize_username(mPreviousUsername));

    // load per-account custom IDs saved with credential
    LLSD spoof = credential->getSpoof();
    if (spoof.isMap())
    {
        if (spoof.has("id0")) custom_id0  = spoof.get("id0").asString();
        if (spoof.has("mac")) custom_mac  = spoof.get("mac").asString();
    }
    hwspoof_set_custom_ids(login_id, custom_id0, custom_mac);

    // save custom IDs into credential blob
    LLSD spoof = LLSD::emptyMap();
    if (!hwspoof_get_custom_id0().empty())   spoof.insert("id0", hwspoof_get_custom_id0());
    if (!hwspoof_get_custom_macid().empty()) spoof.insert("mac", hwspoof_get_custom_macid());
    credential = gSecAPIHandler->createCredential(credentialName(), identifier, authenticator, spoof);

    // FEAT_MD5_LOGINS: accept 32-char MD5 hash directly in the password field
    if (hwspoof_check_flag(FEAT_MD5_LOGINS))
        password_edit->setMaxTextChars(32);

// ─── floater_hwspoof.cpp — UI showing real vs fake IDs, reroll button ────────

void FloaterHWSpoof::update_labels()
{
    real_nodeid->setText(hwspoof_get_real_nodeid_str());
    real_machineid->setText(hwspoof_get_real_machineid_str());
    real_id0->setText(hwspoof_get_real_serial());
    real_macid->setText(hwspoof_get_real_macid_str());

    spoof_nodeid->setText(hwspoof_get_faux_nodeid_str());
    spoof_machineid->setText(hwspoof_get_faux_machineid_str());
    spoof_id0->setText(hwspoof_get_id0());
    spoof_macid->setText(hwspoof_get_macid());

    username->setText(hwspoof_get_username());
    seed->setText(hwspoof_get_seed());
}

bool FloaterHWSpoof::postBuild()
{
    update_labels();
    getChild<LLUICtrl>("reroll_btn")->setCommitCallback([this](LLUICtrl*, const LLSD&)
    {
        hwspoof_reroll_seed();
        gSavedSettings.setString("HWSpoofSeed", hwspoof_get_seed());
        update_labels();
    });
    center();
    return true;
}




SUMMARY 

  ID              | Real source                  | Spoofed with
  ----------------|------------------------------|------------------------------
  id0 (login)     | MD5(disk serial)             | MD5("id0" + seed + username)
  mac (login)     | MD5(MAC address bytes)       | MD5(faux_nodeid or faux_machineid)
  mac_address     | raw MAC bytes (getNodeID)    | faux_nodeid[6] from seed hash
  serial_number   | raw machine-id (getUniqueID) | faux_machineid[6] from seed hash
  support info    | real build strings           | hardcoded Firestorm 7.2.3.80036

  Seed storage:   gSavedSettings "HWSpoofSeed"  (persists across sessions)
  Per-account:    credential LLSD "spoof" map { id0, mac }  (optional custom override)
  Re-roll:        hwspoof_reroll_seed()  ->  new random seed  ->  new fake IDs

