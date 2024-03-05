using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{
    public abstract class ModelBase
    {
        protected ModelBase()
        {

        }
        [Required]
        public string Id { get; set; }
        [Required]
        public string Name { get; set; }

        [Required]
        [NotMapped]
        public Dictionary<string, string> Properties { get; set; } = [];

        public string ToJsonString<T>() where T : ModelBase
        {

            return JsonSerializer.Serialize<T>(this as T);
        }

        public T JsonClone<T>() where T : ModelBase
        {
            T value = this as T;
            var jsonString = JsonSerializer.Serialize<T>(value);
            return JsonSerializer.Deserialize<T>(jsonString);
        }

        public T Clone<T>() where T : ModelBase
        {
            return this.MemberwiseClone() as T;
        }

        public T Copy<T>() where T : ModelBase
        {
            var obj = this.Clone<T>();
            obj.Id = Guid.NewGuid().ToString();
            obj.UpdateProperties();
            return obj;
        }

        protected virtual void UpdateProperties()
        {

        }
    }
}
