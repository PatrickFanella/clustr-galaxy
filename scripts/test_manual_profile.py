import pathlib
import tempfile
import unittest
from manual_acceptance import seed_profile, profile_hashes


class ManualFixtureProfileTests(unittest.TestCase):
    def test_copy_is_independent_and_excludes_v1_and_non_profile_files(self):
        with tempfile.TemporaryDirectory() as root:
            root = pathlib.Path(root)
            source = root / "fixture/profiles/v2"
            (source / "offline-pilot/demo").mkdir(parents=True)
            saved = source / "offline-pilot/demo/9-3.json"
            saved.write_bytes(b'{"fixture":true}')
            (source / "unrelated.txt").write_text("do not copy")
            sentinel = source.parent / "v1-sentinel"
            sentinel.write_bytes(b"legacy untouched")
            selected, before = seed_profile(source, root / "copy")
            copied = root / "copy/unity3d/Subcult/Clustr Galaxy/ClustrGalaxy/profiles/v2"
            self.assertEqual(profile_hashes(copied), before)
            (copied / "offline-pilot/demo/9-3.json").write_text("changed during manual play")
            self.assertEqual(profile_hashes(selected), before)
            self.assertEqual(sentinel.read_bytes(), b"legacy untouched")
            self.assertFalse((copied / "unrelated.txt").exists())
            self.assertFalse((copied.parent / "v1-sentinel").exists())

    def test_rejects_wrong_root_empty_profile_and_symlinks(self):
        with tempfile.TemporaryDirectory() as root:
            root = pathlib.Path(root)
            source = root / "profiles/v2"
            source.mkdir(parents=True)
            with self.assertRaises(ValueError): seed_profile(root / "profiles", root / "copy")
            with self.assertRaises(ValueError): seed_profile(source, root / "copy")
            (source / "outside.json").symlink_to(root / "missing.json")
            with self.assertRaises(ValueError): seed_profile(source, root / "copy")
            self.assertFalse((root / "copy").exists())
