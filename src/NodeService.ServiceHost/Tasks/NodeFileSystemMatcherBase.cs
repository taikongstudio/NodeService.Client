using Newtonsoft.Json.Linq;

namespace NodeService.ServiceHost.Tasks
{
    public abstract class NodeFileSystemMatcherBase
    {

        protected bool ExecuteDateTimeFilters(string filePath, IEnumerable<DateTimeFilter> dateTimeFilters)
        {
            bool isMatched = false;
            var lastWriteTime = File.GetLastWriteTime(filePath);
            var creationTime = File.GetCreationTime(filePath);
            foreach (var dateTimeFilter in dateTimeFilters)
            {
                switch (dateTimeFilter.Kind)
                {
                    case DateTimeFilterKind.DateTime:
                        isMatched = dateTimeFilter.IsMatched(lastWriteTime);
                        break;
                    case DateTimeFilterKind.TimeOnly:
                        isMatched = dateTimeFilter.IsMatched(TimeOnly.FromDateTime(lastWriteTime));
                        break;
                    case DateTimeFilterKind.Days:
                    case DateTimeFilterKind.Hours:
                    case DateTimeFilterKind.Minutes:
                    case DateTimeFilterKind.Seconds:
                        isMatched = dateTimeFilter.IsMatched(DateTime.Now - lastWriteTime);
                        break;
                    default:
                        break;
                }

                if (isMatched)
                {
                    break;
                }
            }
            return isMatched;
        }

        protected bool ExecuteFileLengthFilters(string filePath, IEnumerable<FileLengthFilter> fileLengthFilters)
        {
            var fileInfo = new FileInfo(filePath);
            var matchedCount = 0;
            foreach (var fileLengthFilter in fileLengthFilters)
            {
                var value0 = CalcuateLength(fileLengthFilter.LengthUnit, fileLengthFilter.Values[0]);
                var value1 = CalcuateLength(fileLengthFilter.LengthUnit, fileLengthFilter.Values[1]);
                switch (fileLengthFilter.Operator)
                {
                    case CompareOperator.LessThan:
                        if (fileInfo.Length < value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.GreatThan:
                        if (fileInfo.Length > value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.LessThanEqual:
                        if (fileInfo.Length <= value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.GreatThanEqual:
                        if (fileInfo.Length >= value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.Equals:
                        if (fileInfo.Length == value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.WithinRange:
                        if (fileInfo.Length >= value0 && fileInfo.Length <= value1)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.OutOfRange:
                        if (!(fileInfo.Length >= value0 && fileInfo.Length <= value1))
                        {
                            matchedCount++;
                        }
                        break;
                    default:
                        break;
                }
            }
            return matchedCount > 0;

            static long CalcuateLength(BinaryLengthUnit binaryLengthUnit, double value)
            {
                var length = binaryLengthUnit switch
                {
                    BinaryLengthUnit.Byte => value,
                    BinaryLengthUnit.KB => value * 1024,
                    BinaryLengthUnit.MB => value * 1024 * 1024,
                    BinaryLengthUnit.GB => value * 1024 * 1024 * 1024,
                    BinaryLengthUnit.PB => value * 1024 * 1024 * 1024 * 1024,
                    _ => throw new NotImplementedException(),
                };
                return (long)length;
            }
        }
    }
}