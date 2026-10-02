import pathlib
import tempfile
import unittest
from artifact_identity import identify


class ArtifactIdentityTests(unittest.TestCase):
    def test_managed_code_change_invalidates_identity_even_when_launcher_is_identical(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            player = root / "Player.x86_64"
            player.write_bytes(b"unchanged Unity launcher")
            code = root / "Player_Data/Managed/Game.dll"
            code.parent.mkdir(parents=True)
            code.write_bytes(b"revision one")
            first = identify(player)
            code.write_bytes(b"revision two")
            second = identify(player)
            self.assertNotEqual(first["tree_sha256"], second["tree_sha256"])
            self.assertEqual(first["files_sha256"][player.name], second["files_sha256"][player.name])
