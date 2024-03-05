using JobsWorker.Shared.DataModels;
using System.Linq.Expressions;

namespace JobsWorkerWebService.Data
{
    public static partial class  DbContextExtensions
    {
        public static IEnumerable<T> UpdateBindingCollection<T>(
            this ApplicationDbContext dbContext, IList<T> dest, IEnumerable<T> src) where T : BindingModelBase
        {
            ArgumentNullException.ThrowIfNull(dest, nameof(dest));
            ArgumentNullException.ThrowIfNull(src, nameof(src));
            if (dest.Count == 0 && !src.Any())
            {
                return Array.Empty<T>();
            }
            List<T> removeList = [];
            foreach (var item in dest)
            {
                if (!src.Any(y => y.CombinedKey == item.CombinedKey))
                {
                    removeList.Add(item);
                }
            }
            foreach (var item in removeList)
            {
                dest.Remove(item);
            }
            foreach (var item in src)
            {
                var binding = dest.FirstOrDefault(x => x.CombinedKey == item.CombinedKey);
                if (binding == null)
                {
                    dest.Add(item);
                    continue;
                }
                binding.TargetForeignKey = item.TargetForeignKey;
            }
            dbContext.RemoveRange(removeList);
            return removeList;
        }

        public static IEnumerable<T> UpdateCollection<T>(
            this ApplicationDbContext dbContext, IList<T> dest, IEnumerable<T> src, Action<T> addOrUpdateAction, Action<T> removeAction) where T : ModelBase
        {
            ArgumentNullException.ThrowIfNull(dest, nameof(dest));
            ArgumentNullException.ThrowIfNull(src, nameof(src));
            List<T> removeList = [];
            foreach (var item in dest)
            {
                if (!src.Any(y => y.Id == item.Id))
                {
                    removeList.Add(item);
                }
            }
            foreach (var item in removeList)
            {
                dest.Remove(item);
            }
            foreach (var item in src)
            {
                var model = dest.FirstOrDefault(x => x.Id == item.Id);
                if (model == null)
                {
                    addOrUpdateAction.Invoke(item);
                    dest.Add(item);
                    continue;
                }
                model.Id = item.Id;
                addOrUpdateAction.Invoke(item);
            }
            foreach (var item in removeList)
            {
                removeAction.Invoke(item);
            }
            return removeList;
        }

        public static async Task<IEnumerable<T>> UpdateCollectionAsync<T>(
    this ApplicationDbContext dbContext, IList<T> dest, IEnumerable<T> src, Func<T, Task> addOrUpdateFunc, Func<T, Task> removeFunc) where T : ModelBase
        {
            ArgumentNullException.ThrowIfNull(dest, nameof(dest));
            ArgumentNullException.ThrowIfNull(src, nameof(src));
            List<T> removeList = [];
            foreach (var item in dest)
            {
                if (!src.Any(y => y.Id == item.Id))
                {
                    removeList.Add(item);
                }
            }
            foreach (var item in removeList)
            {
                dest.Remove(item);
            }
            foreach (var item in src)
            {
                var model = dest.FirstOrDefault(x => x.Id == item.Id);
                if (model == null)
                {
                    await addOrUpdateFunc.Invoke(item);
                    dest.Add(item);
                    continue;
                }
                model.Id = item.Id;
                await addOrUpdateFunc.Invoke(item);
            }
            foreach (var item in removeList)
            {
                await removeFunc.Invoke(item);
            }
            return removeList;
        }

    }
}
