namespace JulyArch
{
    public readonly struct StoreModifiedEvent
    {
        public readonly StoreBase Store;
        public readonly string Method;

        public StoreModifiedEvent(StoreBase store, string method)
        {
            Store = store;
            Method = method;
        }

        public override string ToString() => $"[Store] {Store?.GetType().Name}.{Method}()";
    }
}