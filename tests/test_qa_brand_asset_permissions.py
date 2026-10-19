"""Regression contract for institutional brand assets inside QA Nginx image.

The four files are intentionally public, never secrets. The image's final stage
must normalize permissions so unprivileged Nginx can serve them (issue #553).
"""
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
DOCKERFILE = (ROOT / "frontend/Dockerfile").read_text()
BRAND = ROOT / "frontend/public/brand"
LOGO_NAME = "logo-glmch-reducido-v2-azul.svg"
REQUIRED = (
    LOGO_NAME,
    "favicon-glmch.svg",
    "favicon-32.png",
    "apple-touch-icon.png",
)


class BrandPublicFilesTest(unittest.TestCase):
    def test_brand_sources_exist(self):
        for name in REQUIRED:
            with self.subTest(name=name):
                path = BRAND / name
                self.assertTrue(path.is_file(), f"Falta marca pública: {path}")
                self.assertGreater(path.stat().st_size, 0)

    def test_nginx_final_stage_normalizes_only_brand_files(self):
        marker = "COPY --from=build /app/dist /usr/share/nginx/html"
        self.assertIn(marker, DOCKERFILE)
        self.assertIn("chmod 0755 /usr/share/nginx/html/brand", DOCKERFILE)
        self.assertIn(
            "find /usr/share/nginx/html/brand -maxdepth 1 -type f -exec chmod 0644 {} +",
            DOCKERFILE,
        )
        self.assertLess(
            DOCKERFILE.index(marker),
            DOCKERFILE.index("chmod 0755 /usr/share/nginx/html/brand"),
        )
        self.assertNotIn("chmod -R 777", DOCKERFILE)

    def test_all_four_files_are_part_of_image_contract(self):
        for name in REQUIRED:
            with self.subTest(name=name):
                self.assertIn("/usr/share/nginx/html/brand/" + name, DOCKERFILE)

    def test_login_and_nav_use_repo_logo(self):
        login = (ROOT / "frontend/src/auth/AccessScreen.tsx").read_text()
        app = (ROOT / "frontend/src/App.tsx").read_text()
        self.assertIn("brand/" + LOGO_NAME, login)
        self.assertIn("brand/" + LOGO_NAME, app)


if __name__ == "__main__":
    unittest.main()
