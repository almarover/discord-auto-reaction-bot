import unittest

from bot import select_reactions
from config_store import ReactionPlan, reaction_plan_for_message


class ChannelFilteringTests(unittest.TestCase):
    def setUp(self) -> None:
        self.config = {
            "version": 3,
            "guilds": {
                "1": {
                    "enabled": True,
                    "common_emoji_ids": ["101"],
                    "common_channel_ids": ["10"],
                    "targets": [
                        {
                            "user_id": "2",
                            "emoji_ids": ["202"],
                            "channel_ids": ["20"],
                        }
                    ],
                }
            },
        }

    def test_public_and_dedicated_channels_are_independent(self) -> None:
        public_plan = reaction_plan_for_message(self.config, 1, 2, 10)
        self.assertEqual(public_plan.common_emoji_ids, (101,))
        self.assertEqual(public_plan.dedicated_emoji_ids, ())

        dedicated_plan = reaction_plan_for_message(self.config, 1, 2, 20)
        self.assertEqual(dedicated_plan.common_emoji_ids, ())
        self.assertEqual(dedicated_plan.dedicated_emoji_ids, (202,))

        self.assertIsNone(reaction_plan_for_message(self.config, 1, 2, 30))

    def test_empty_channel_lists_mean_all_channels(self) -> None:
        guild = self.config["guilds"]["1"]
        guild["common_channel_ids"] = []
        guild["targets"][0]["channel_ids"] = []
        plan = reaction_plan_for_message(self.config, 1, 2, 999)
        self.assertEqual(plan.emoji_ids, (101, 202))


class RandomSelectionTests(unittest.TestCase):
    @staticmethod
    def choose_last(population, *, weights, k):
        return [population[-1]]

    def test_public_and_dedicated_random_draws_are_independent(self) -> None:
        plan = ReactionPlan(
            common_emoji_ids=(1, 2),
            dedicated_emoji_ids=(3, 4),
            target_label="test",
            random_enabled=True,
            weights=(25, 75),
            common_random_enabled=True,
            common_weights=(40, 60),
        )
        selected = select_reactions(plan, chooser=self.choose_last)
        self.assertEqual(selected.reactions, (2, 4))
        self.assertAlmostEqual(selected.common.selected_probability, 60)
        self.assertAlmostEqual(selected.dedicated.selected_probability, 75)

    def test_no_reaction_probability_can_skip_each_pool(self) -> None:
        plan = ReactionPlan(
            common_emoji_ids=(1,),
            dedicated_emoji_ids=(2,),
            target_label="test",
            random_enabled=False,
            weights=(100,),
            no_reaction_enabled=True,
            no_reaction_probability=50,
            common_no_reaction_enabled=True,
            common_no_reaction_probability=50,
        )
        selected = select_reactions(plan, random_value=lambda: 0.49)
        self.assertEqual(selected.reactions, ())
        self.assertTrue(selected.common.skipped)
        self.assertTrue(selected.dedicated.skipped)

    def test_non_random_mode_outputs_all_reactions_after_trigger(self) -> None:
        plan = ReactionPlan(
            common_emoji_ids=(1, 2),
            dedicated_emoji_ids=(3, 4),
            target_label="test",
            random_enabled=False,
            weights=(),
            no_reaction_enabled=True,
            no_reaction_probability=50,
        )
        selected = select_reactions(plan, random_value=lambda: 0.75)
        self.assertEqual(selected.reactions, (1, 2, 3, 4))


if __name__ == "__main__":
    unittest.main()
