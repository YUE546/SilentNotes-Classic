// net40 compat: copy of SilentNotes.AllPlatforms\Models\DeletedNoteModel.cs, differs from the
// frozen source in two points to stay within C# 6:
// - adds the using directives which the frozen file takes from the C#10 global usings file
// - replaces the C#12 primary constructor of DeletedNoteModelIdComparer with a plain class declaration

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;

namespace SilentNotes.Models
{
    /// <summary>
    /// Information about deleted notes (removed even from the recycle bin), so they can be cleaned
    /// up across multiple devices.
    /// </summary>
    [DebuggerDisplay("{Id} {DeletedAt}")]
    public class DeletedNoteModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeletedNoteModel"/> class.
        /// Parameterless constructor is required for deserialization.
        /// </summary>
        public DeletedNoteModel()
        {
            DeletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeletedNoteModel"/> class.
        /// </summary>
        /// <param name="id">Sets the <see cref="Id"/> property.</param>
        public DeletedNoteModel(Guid id)
            : this()
        {
            Id = id;
        }

        /// <summary>
        /// Makes <paramref name="target"/> a deep copy of this instance.
        /// </summary>
        /// <remarks>
        /// If the instance is the same as the target, no copy is performed.
        /// </remarks>
        /// <param name="target">Copy all properties to this note.</param>
        public void CloneTo(DeletedNoteModel target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (Object.ReferenceEquals(this, target))
                return;

            target.Id = Id;
            target.DeletedAt = DeletedAt;
        }

        /// <summary>
        /// Gets or sets the id of the deleted note.
        /// </summary>
        [XmlText]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the time in UTC, when the deletion took place. This date is important,
        /// when a note is imported from another app repeatedly, so it can be re-imported even
        /// after the note was deleted by the user.
        /// </summary>
        [XmlAttribute(AttributeName = "deleted_at")]
        public DateTime DeletedAt { get; set; }

        /// <summary>
        /// Makes a deep copy of the instance.
        /// </summary>
        /// <returns>Copy of the instance.</returns>
        public DeletedNoteModel Clone()
        {
            var result = new DeletedNoteModel();
            CloneTo(result);
            return result;
        }
    }

    /// <summary>
    /// Provides a comparer for <see cref="DeletedNoteModel"/> objects that compares them based on
    /// their Id property.
    /// </summary>
    public class DeletedNoteModelIdComparer : IComparer<DeletedNoteModel>
    {
        public int Compare(DeletedNoteModel x, DeletedNoteModel y)
        {
            return x.Id.CompareTo(y.Id);
        }
    }
}
