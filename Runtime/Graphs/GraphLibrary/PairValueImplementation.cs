namespace GraphLibrary
{
    using System;

    public class DirectedPairValue<T> : IPairValue<T>
    {
        private readonly T _t1;
        private readonly T _t2;

        public DirectedPairValue(T t1, T t2)
        {
            if (t1 == null || t2 == null)
                throw new ArgumentNullException();
            if (t1.GetType() != t2.GetType())
                throw new ArgumentException();

            _t1 = t1;
            _t2 = t2;
        }

        public bool Contains(T value)
        {
            return value.Equals(_t1) || value.Equals(_t2);
        }

        public T GetFirst()
        {
            return _t1;
        }

        public T GetSecond()
        {
            return _t2;
        }

        // Override Equals implementation
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(DirectedPairValue<T>))
                return false;
            DirectedPairValue<T> casted = (DirectedPairValue<T>)obj;
            return casted._t1.Equals(_t1) && casted._t2.Equals(_t2);
        }

        // Return sum of vertex hash codes
        public override int GetHashCode()
        {
            return _t1.GetHashCode() + _t2.GetHashCode();
        }
    }

    // Same as directed with different Equals override, TODO: merge somehow
    public class UndirectedPairValue<T> : IPairValue<T>
    {
        private readonly T _t1;
        private readonly T _t2;

        public UndirectedPairValue(T t1, T t2)
        {
            if (t1 == null || t2 == null)
                throw new ArgumentNullException();
            if (t1.GetType() != t2.GetType())
                throw new ArgumentException();

            _t1 = t1;
            _t2 = t2;
        }

        public bool Contains(T value)
        {
            return value.Equals(_t1) || value.Equals(_t2);
        }

        public T GetFirst()
        {
            return _t1;
        }

        public T GetSecond()
        {
            return _t2;
        }

        // Override Equals implementation
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(UndirectedPairValue<T>))
                return false;
            UndirectedPairValue<T> casted = (UndirectedPairValue<T>)obj;
            return (casted._t1.Equals(_t1) && casted._t2.Equals(_t2)) 
                || (casted._t1.Equals(_t2) && casted._t2.Equals(_t1));
        }

        // Return sum of vertex hash codes
        public override int GetHashCode()
        {
            return _t1.GetHashCode() + _t2.GetHashCode();
        }
    }
}

