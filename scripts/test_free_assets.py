"""License/source integrity for the curated free assets; no Unity license required."""
import hashlib
import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets/ClustrGalaxy/Presentation/FreeAssets"

class FreeAssetIntegrityTests(unittest.TestCase):
    def test_every_selected_original_matches_its_recorded_hash(self):
        records = json.loads((ASSETS / "provenance.json").read_text())["selectedAssets"]
        self.assertEqual(len(records), 25)  # 20 asset files and five original Kenney licenses.
        self.assertEqual(len({item["file"] for item in records}), len(records))
        for item in records:
            with self.subTest(file=item["file"]):
                path = ASSETS / item["file"]
                self.assertTrue(path.resolve().is_relative_to(ASSETS.resolve()))
                self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), item["sha256"])
                self.assertIn(item["license"], ("CC0-1.0", "CC-BY-4.0"))
                self.assertTrue(item["source"].startswith(("https://kenney.nl/assets/", "https://www.solarsystemscope.com/textures/download/")))
                self.assertTrue(path.with_name(path.name + ".meta").exists())

    def test_licenses_and_attribution_are_packaged_in_source(self):
        for pack in ("space-kit", "space-station-kit", "sci-fi-sounds", "interface-sounds", "input-prompts"):
            self.assertIn("CC0", (ASSETS / pack / "License.txt").read_text())
        notice = (ASSETS / "SolarSystemScope/License.txt").read_text()
        self.assertIn("https://creativecommons.org/licenses/by/4.0/", notice)
        self.assertIn("INOVE", notice)
        self.assertTrue((ROOT / "docs/free-asset-notices.md").exists())

    def test_curated_originals_contain_no_executable_asset_content(self):
        records = json.loads((ASSETS / "provenance.json").read_text())["selectedAssets"]
        for item in records:
            self.assertIn(pathlib.Path(item["file"]).suffix.lower(), (".fbx", ".png", ".jpg", ".ogg", ".txt"))

if __name__ == "__main__":
    unittest.main()
