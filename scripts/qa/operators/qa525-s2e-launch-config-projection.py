import json, os, re, stat, sys
P = "/opt/centenario/qa525-root-source/da274d651d8e9e056525413406c0aab62131504f/launch-config.json"
ALLOW = ("inbox", "temp_root", "socket_path", "journal_root", "lease_dir")
fd = os.open(P, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
st = os.fstat(fd)
if not stat.S_ISREG(st.st_mode) or st.st_uid != 0 or st.st_nlink != 1 or stat.S_IMODE(st.st_mode) & 0o022 or st.st_size > 8192:
    print("S2E_STOP=FILE_META"); sys.exit(1)
raw = os.read(fd, st.st_size + 1)
if len(raw) != st.st_size:
    print("S2E_STOP=SHORT"); sys.exit(1)
doc = json.loads(raw)
if not isinstance(doc, dict):
    print("S2E_STOP=SHAPE"); sys.exit(1)
print("S2E_FILE_SHA256=" + __import__("hashlib").sha256(raw).hexdigest(), "SIZE=%d" % len(raw))
print("S2E_TOP_LEVEL_KEYS=" + ",".join(sorted(k for k in doc if re.fullmatch(r"[a-z0-9_]{1,40}", k))))
for k in ALLOW:
    v = doc.get(k)
    print("S2E_%s=%s" % (k.upper(), v if isinstance(v, str) and re.fullmatch(r"/[A-Za-z0-9_./-]{1,200}", v) else "ABSENT_OR_INVALID"))
pins = doc.get("g6_pins")
if isinstance(pins, dict):
    for k in sorted(pins):
        v = pins[k]
        print("S2E_G6PIN_%s=%s" % (k, v if isinstance(k, str) and re.fullmatch(r"[a-z0-9_]{1,40}", k) and isinstance(v, str) and re.fullmatch(r"(sha256:)?[0-9a-f]{64}|/[A-Za-z0-9_./-]{1,200}", v) else "NOT_SHOWN"))
else:
    print("S2E_G6_PINS=ABSENT")
