import unittest
from network_proof import metric


class NetworkMetricTests(unittest.TestCase):
    def test_native_serialization_label(self):
        self.assertEqual(metric('robust_game_state_update_usage_sum{area="Serialize States"} 0.25\n',
                                'robust_game_state_update_usage_sum', '{area="Serialize States"}'), .25)

    def test_missing_counter_rejected(self):
        with self.assertRaisesRegex(AssertionError, "Missing or ambiguous"):
            metric('# HELP robust_net_sent_bytes test\n', 'robust_net_sent_bytes')

    def test_nan_rejected(self):
        with self.assertRaisesRegex(AssertionError, "Invalid metric"):
            metric('robust_net_sent_bytes NaN\n', 'robust_net_sent_bytes')

    def test_duplicate_counter_rejected(self):
        with self.assertRaisesRegex(AssertionError, "Missing or ambiguous"):
            metric('robust_net_sent_bytes 1\nrobust_net_sent_bytes 2\n', 'robust_net_sent_bytes')


if __name__ == '__main__':
    unittest.main()
