namespace JulyArch
{
    public static class ArchServices
    {
        public static IArchLogger Logger { get; private set; } = new DefaultArchLogger();
        public static IArchSave Save { get; private set; }

        public static void Init(IArchLogger logger = null, IArchSave save = null)
        {
            if (logger != null) Logger = logger;
            if (save != null) Save = save;
        }

        public static void Reset()
        {
            Logger = new DefaultArchLogger();
            Save = null;
        }
    }
}
