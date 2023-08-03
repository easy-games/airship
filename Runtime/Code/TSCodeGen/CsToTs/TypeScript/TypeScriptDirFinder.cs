using System.Collections.Generic;
using System.IO;

namespace CsToTs.TypeScript
{
    public static class TypeScriptDirFinder
    {
        public static string FindTypeScriptDirectory()
        {
            var queue = new Queue<string>();
            queue.Enqueue("Assets");

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                var tsDir = Path.Join(dir, "Typescript~");

                if (Directory.Exists(tsDir))
                {
                    return tsDir;
                }

                var subDirs = Directory.GetDirectories(dir);
                foreach (var subDir in subDirs)
                {
                    queue.Enqueue(subDir);
                }
            }

            return null;
        }
    }
}
