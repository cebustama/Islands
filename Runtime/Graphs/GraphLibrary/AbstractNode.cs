using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GraphLibrary
{
    public abstract class AbstractNode : IVertex
    {
        public abstract string GetName();
        public abstract int GetID();
    }

    public class GenericNode : AbstractNode
    {
        public string Name; //{ get; private set; }

        public override string GetName()
        {
            return Name;
        }

        public override int GetID()
        {
            return Name.GetHashCode();
        }
    }

}
