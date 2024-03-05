using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace JobsWorker.Shared.DataModels
{
    public abstract class EditableObject<T>: IEditableObject where T : class
    {
        [NotMapped]
        [JsonIgnore]
        protected T? EditCopy { get; set; }

        [JsonIgnore]
        [NotMapped]
        public bool IsEditing { get; set; }

        public void BeginEdit()
        {
            this.EditCopy = this.MakeCopy();
            this.IsEditing = true;
        }

        public void CancelEdit()
        {
            this.Restore();
            this.IsEditing = false;
        }

        public void EndEdit()
        {
            this.EditCopy = null;
            this.IsEditing = false;
        }

        protected abstract T MakeCopy();

        protected abstract void Restore();
    }
}