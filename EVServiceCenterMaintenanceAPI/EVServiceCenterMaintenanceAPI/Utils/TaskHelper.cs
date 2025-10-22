namespace EVServiceCenterMaintenanceAPI.Utils
{
    public static class TaskHelper
    {
        //No parameter
        public static void FireAndForget(Func<Task> taskFunc)
        {
            Task.Run(async () =>
            {
                try
                {
                    await taskFunc();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FireAndForget error: {ex}");
                }
            });
        }

        // 1 parameter
        public static void FireAndForget<T>(T arg, Func<T, Task> taskFunc)
        {
            Task.Run(async () =>
            {
                try
                {
                    await taskFunc(arg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FireAndForget error: {ex}");
                }
            });
        }

        // 2 parameter
        public static void FireAndForget<T1, T2>(T1 arg1, T2 arg2, Func<T1, T2, Task> taskFunc)
        {
            Task.Run(async () =>
            {
                try
                {
                    await taskFunc(arg1, arg2);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FireAndForget error: {ex}");
                }
            });
        }

        // 3 parameter
        public static void FireAndForget<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3, Func<T1, T2, T3, Task> taskFunc)
        {
            Task.Run(async () =>
            {
                try
                {
                    await taskFunc(arg1, arg2, arg3);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FireAndForget error: {ex}");
                }
            });
        }

        // 4 parameter
        public static void FireAndForget<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, Func<T1, T2, T3, T4, Task> taskFunc)
        {
            Task.Run(async () =>
            {
                try
                {
                    await taskFunc(arg1, arg2, arg3, arg4);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FireAndForget error: {ex}");
                }
            });
        }
    }
}
