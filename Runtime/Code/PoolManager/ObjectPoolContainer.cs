namespace Code.PoolManager
{
    public class ObjectPoolContainer<T>
    {
        private T item;

        public bool Used { get; private set; }

        public bool Destroyed;

        public void Consume()
        {
            Used = true;
        }

        public T Item
        {
            get
            {
                return item;
            }
            set
            {
                item = value;
            }
        }

        public void Release()
        {
            Used = false;
        }
    }
}