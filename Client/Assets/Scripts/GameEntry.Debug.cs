
    public partial class GameEntry
    {
        public static void ClearScreenLog()
        {
            Instance.GetComponent<ConsoleToScreen>().ClearLog();
        }
    }
