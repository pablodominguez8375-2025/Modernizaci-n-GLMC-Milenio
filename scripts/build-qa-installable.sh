#!/usr/bin/env bash
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
SOURCE_SHA="${SOURCE_SHA:-${GITHUB_SHA:-$(git -C "$ROOT" rev-parse HEAD)}}"
SOURCE_REF="${SOURCE_REF:-${GITHUB_REF_NAME:-dev}}"
RUN_ID="${BUILD_RUN_ID:-${GITHUB_RUN_ID:-local}}"
OUT_DIR="${QA_PACKAGE_OUT_DIR:-$ROOT/artifacts/qa-installable}"
SHORT_SHA="${SOURCE_SHA:0:12}"
PACKAGE_NAME="Proyecto-Centenario-QA-srv01-${SHORT_SHA}"
STAGING="$(mktemp -d)"

cleanup(){ rm -rf "$STAGING"; }
trap cleanup EXIT

command -v git >/dev/null 2>&1 || { echo "git es obligatorio" >&2; exit 1; }
command -v tar >/dev/null 2>&1 || { echo "tar es obligatorio" >&2; exit 1; }
command -v zip >/dev/null 2>&1 || { echo "zip es obligatorio" >&2; exit 1; }
command -v sha256sum >/dev/null 2>&1 || { echo "sha256sum es obligatorio" >&2; exit 1; }

mkdir -p "$OUT_DIR" "$STAGING/$PACKAGE_NAME"

git -C "$ROOT" cat-file -e "$SOURCE_SHA^{commit}"
git -C "$ROOT" archive --format=tar "$SOURCE_SHA" | tar -xf - -C "$STAGING/$PACKAGE_NAME"

cat > "$STAGING/$PACKAGE_NAME/INSTALAR.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec bash "$ROOT/scripts/install-srv01-qa.sh" "$@"
EOF
chmod +x "$STAGING/$PACKAGE_NAME/INSTALAR.sh"

cp "$STAGING/$PACKAGE_NAME/docs/installation/QA-SRV01-INSTALL.md" "$STAGING/$PACKAGE_NAME/LEAME-INSTALACION.md"
cp "$STAGING/$PACKAGE_NAME/docs/installation/VM-IMPLEMENTATION-CHECKLIST.md" "$STAGING/$PACKAGE_NAME/CHECKLIST-IMPLEMENTACION.md"

cat > "$STAGING/$PACKAGE_NAME/BUILD-INFO.txt" <<EOF
PROJECT=Proyecto Centenario
CHANNEL=QA-srv01
SOURCE_SHA=$SOURCE_SHA
SOURCE_REF=$SOURCE_REF
BUILD_RUN_ID=$RUN_ID
GENERATED_AT_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)
EOF

cat > "$STAGING/$PACKAGE_NAME/ESTADO-PAQUETE.txt" <<EOF
Paquete QA operacional para srv01.
Datos personales reales: PROHIBIDOS.
DemoData: habilitado sólo para datos ficticios QA.
El paquete no implica despliegue ni UAT completados.
Verificar BUILD-INFO.txt y MANIFEST.sha256 antes de instalar.
EOF

git -C "$ROOT" bundle create "$STAGING/$PACKAGE_NAME/pmgm-repository.bundle" --all

(
  cd "$STAGING/$PACKAGE_NAME"
  find . -type f ! -name MANIFEST.sha256 -print0 | LC_ALL=C sort -z | xargs -0 sha256sum > MANIFEST.sha256
  sha256sum -c MANIFEST.sha256 >/dev/null
)

rm -f "$OUT_DIR/Proyecto-Centenario-QA-srv01-"*.zip
(
  cd "$STAGING"
  zip -qr "$OUT_DIR/$PACKAGE_NAME.zip" "$PACKAGE_NAME"
)

unzip -t "$OUT_DIR/$PACKAGE_NAME.zip" >/dev/null
printf 'QA INSTALLABLE OK — %s\n' "$OUT_DIR/$PACKAGE_NAME.zip"
