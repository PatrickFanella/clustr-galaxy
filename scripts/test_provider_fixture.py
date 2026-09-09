import unittest
from provider_fixture import document, LIMITS

class FixtureContract(unittest.TestCase):
    def test_separate_systems_have_distinct_child_identities(self):
        a,_=document("system","subreddit:a",False)
        b,_=document("system","subreddit:b",False)
        self.assertFalse({n["id"] for n in a} & {n["id"] for n in b})

    def test_stress_populations_and_unique_identities(self):
        for scope,(nodes_max,links_max,_,_) in LIMITS.items():
            nodes,links=document(scope,"comment:fixture-3" if scope == "thread" else "fixture:root",True)
            self.assertEqual(len(nodes),nodes_max)
            self.assertEqual(len(links),links_max)
            self.assertEqual(len({node["id"] for node in nodes}),nodes_max)
            self.assertEqual(len({(link["source"],link["target"],link["type"]) for link in links}),links_max)
            ids={node["id"] for node in nodes}
            self.assertTrue(all(link["source"] in ids and link["target"] in ids for link in links))
            if scope == "thread": self.assertTrue(all(node["metrics"]["depth"] <= 3 for node in nodes))

if __name__ == "__main__": unittest.main()
