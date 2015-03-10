using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary2
{
    public class Trie
    {
        private Node RootNode;

        public Trie()
        {
            RootNode = new Node(Node.Root);
        }

        public void Add(string word)
        {
            word = word.ToLower() + Node.Eow;
            var currentNode = RootNode;
            foreach (var c in word)
            {
                currentNode = currentNode.AddChild(c);
            }
        }

        public List<string> Match(string prefix, int maxMatches)
        {
            prefix = prefix.ToLower();

            var set = new HashSet<string>();

            _MatchRecursive(RootNode, set, "", prefix, maxMatches);
            return set.ToList();

        }

        private static void _MatchRecursive(Node node, ISet<string> rtn, string letters, string prefix, int maxMatches)
        {
            if (rtn.Count == maxMatches)
            {
                return;
            }

            if (node == null)
            {
                if (!rtn.Contains(letters))
                {
                    rtn.Add(letters);
                    return;
                }
            }

            letters += node.Letter.ToString();

            if (prefix.Length > 0)
            {
                if (node.ContainsKey(prefix[0]))
                {
                    _MatchRecursive(node[prefix[0]], rtn, letters, prefix.Remove(0, 1), maxMatches);
                }
            }
            else
            {
                foreach (char key in node.Keys)
                {
                    _MatchRecursive(node[key], rtn, letters, prefix, maxMatches);
                }
            }
        }
    }
}
