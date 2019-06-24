using System.Collections.Generic;
using System.Linq;

namespace GVFS.Tests
{
    public class DataSources
    {
        public static object[] AllBools
        {
            get
            {
                return new object[]
                {
                     new object[] { true },
                     new object[] { false },
                };
            }
        }

        public static object[] IntegerModes(int num)
        {
            IEnumerable<object> GetModes(int n)
            {
                for (int i = 0; i < n; i++)
                {
                    yield return new object[] { i };
                }
            }

            return GetModes(num).ToArray();
        }
    }
}
