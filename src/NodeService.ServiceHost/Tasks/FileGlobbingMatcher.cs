using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.ServiceHost.Tasks
{
    public class FileGlobbingMatcher:NodeFileSystemMatcherBase
    {
        Matcher _matcher;
        public FileGlobbingMatcher(StringComparison stringComparison)
        {
            _matcher = new Matcher(stringComparison);
        }

        public IEnumerable<string> Match(
            string directoryPath,
            IEnumerable<string> includes,
            IEnumerable<string> excludes,
            IEnumerable<DateTimeFilter> dateTimeFilters,
            IEnumerable<FileLengthFilter> fileLengthFilters)
        {
            _matcher.AddIncludePatterns(includes);
            _matcher.AddExcludePatterns(excludes);
            var filePathList = _matcher.GetResultsInFullPath(directoryPath);
            foreach (var filePath in filePathList)
            {
                if (ExecuteDateTimeFilters(filePath, dateTimeFilters) && ExecuteFileLengthFilters(filePath, fileLengthFilters))
                {
                    yield return filePath;
                }
            }
            yield break;
        }


    }
}
