using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Web;
using System.Collections;

namespace ClassLibrary2
{
    public class Node
    {
        public const char Eow = '$';
        public const char Root = ' ';

        public char Letter { get; set; }
        public HybridDictionary Children { get; private set; }

        public Node(char letter)
        {
            this.Letter = letter;
        }

        public Node this[char index]
        {
            get { return (Node)Children[index]; }
        }

        public ICollection Keys
        {
            get { return Children.Keys; }
        }

        public bool ContainsKey(char key)
        {
            return Children.Contains(key);
        }

        public Node AddChild(char letter)
        {
            if (Children == null)
                Children = new HybridDictionary();

            if (!Children.Contains(letter))
            {
                var node = letter != Eow ? new Node(letter) : null;
                Children.Add(letter, node);
                return node;
            }

            return (Node)Children[letter];
        }

    }
}
