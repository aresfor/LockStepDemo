
    public class Singleton<T> where T: new()
    {
        private static T m_Instance;
        public static T Instance
        {
            get
            {
                if (null == m_Instance)
                {
                    m_Instance = new T();
                }

                return m_Instance;
            }
        }
    }
