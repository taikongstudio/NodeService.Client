using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{

    public class StringEntry : EditableObject<StringEntry>
    {
        public StringEntry()
        {

        }

        public StringEntry(string name, string? value)
        {
            this.Id = Guid.NewGuid().ToString();
            this.Name = name;
            this.Value = value;
        }

        [NotMapped]
        public string Id { get; set; }

        public string? Name { get; set; }

        public string? Value { get; set; }

        public string? Tag { get; set; }

        protected override void Restore()
        {
            this.Name = this.EditCopy?.Name;
            this.Value = this.EditCopy?.Value;
        }

        protected override StringEntry MakeCopy()
        {
            return new StringEntry { Id = Id, Name = Name, Value = Value, IsEditing = true };
        }
    }
}
