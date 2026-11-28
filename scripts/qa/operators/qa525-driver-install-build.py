#!/usr/bin/env python3
"""Build qa525-driver-install.py = template + the EMBEDDED final driver + the 11-library pin table (reproducible)."""
import base64
import hashlib
import importlib.util
import pathlib

DRIVER_FILE = "qa525-manual-cut-e5c40d3a.py"
MARK_D = 'DRIVER = ("", 0, ())  # @@DRIVER@@'
MARK_L = "LIBS = {}  # @@LIBS@@"


def lib_pins(driver_path):
    spec = importlib.util.spec_from_file_location("drv_for_pins", driver_path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    pins = {**mod.LIB_PINS, **mod.G6_PINS}
    if len(pins) != 11 or any(type(v) is not str or len(v) != 64 for v in pins.values()):
        raise SystemExit("driver pins invalid")
    return pins


def build(template, driver_path):
    data = pathlib.Path(driver_path).read_bytes()
    b64 = base64.b64encode(data).decode()
    lines = ",\n".join("    %r" % b64[i:i + 76] for i in range(0, len(b64), 76))
    drv = "DRIVER = (%r, %d, (\n%s,\n))" % (hashlib.sha256(data).hexdigest(), len(data), lines)
    libs = "LIBS = {\n" + "".join("    %r: %r,\n" % (n, h) for n, h in sorted(lib_pins(driver_path).items())) + "}"
    if template.count(MARK_D) != 1 or template.count(MARK_L) != 1:
        raise SystemExit("template marker missing")
    return template.replace(MARK_D, drv).replace(MARK_L, libs)


if __name__ == "__main__":
    here = pathlib.Path(__file__).resolve().parent
    out = build((here / "qa525-driver-install.template.py").read_text(), here / DRIVER_FILE)
    (here / "qa525-driver-install.py").write_text(out)
    print(hashlib.sha256(out.encode()).hexdigest())
