using System;

namespace de.creinbold.FlatShare
{
    [System.Serializable]
    public class IntegrityException : Exception
    {
        public IntegrityException() { }
        public IntegrityException(string message) : base(message) { }
        public IntegrityException(string message, Exception inner) : base(message, inner) { }
        protected IntegrityException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
