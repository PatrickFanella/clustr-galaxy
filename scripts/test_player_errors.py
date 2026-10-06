import unittest
from unity_process import player_errors


class PlayerErrorsTests(unittest.TestCase):
    def test_native_unity_message_signature_error_is_not_a_passing_player(self):
        self.assertTrue(player_errors("Script error (ExpeditionController): Start() can not take parameters."))
        self.assertTrue(player_errors("NullReferenceException: missing state"))
        self.assertTrue(player_errors("Assertion failed on expression: renderer"))
        self.assertFalse(player_errors("[ClustrGalaxy.DemoJourney] PASS create"))
