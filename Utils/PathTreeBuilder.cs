// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PathTree
{
    /// <summary>
    /// Simple tree builder, with provisions for factories to build external trees.
    /// </summary>
    /// <typeparam name="PathKeyType"></typeparam>
    /// <typeparam name="ValueType"></typeparam>
    public class PathTreeBuilder<PathKeyType,ValueType>
    {
        class Node
        {
            public PathKeyType PathKey { get; internal set; }
            public ValueType Val { get; internal set; }
            public List<Node> Children { get; internal set; } = new List<Node>();
        }

        Node Root = new Node();

        public void Clear() { Root = new Node(); }

        public void Insert(IEnumerable<PathKeyType> path, ValueType val)
        {
            Node currentNode = Root;
            foreach (PathKeyType pathPart in path)
            {
                Node node = currentNode.Children.FirstOrDefault(n => n.PathKey.Equals(pathPart));
                if (node == null)
                {
                    node = new Node() { PathKey = pathPart };
                    currentNode.Children.Add(node);
                    currentNode = node;
                }
                else
                {
                    currentNode = node;
                }
            }
            currentNode.Val = val;
        }

        private IEnumerable<T> BuildDepthFirst<T>(Node node, T parent, Func<T, PathKeyType, ValueType, T> buildFn)
        {
            if (node != Root)
            {
                parent = buildFn(parent, node.PathKey, node.Val);
                yield return parent;
            }
            
            foreach (var child in node.Children)
            {
                foreach (T v in BuildDepthFirst(child, parent, buildFn))
                {
                    yield return v;
                }
            }
        }

        public IEnumerable<T> BuildDepthFirst<T>(T seed, Func<T, PathKeyType, ValueType, T> buildFn) => BuildDepthFirst(Root, seed, buildFn).ToList();
    }
}
