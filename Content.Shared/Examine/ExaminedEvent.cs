using Robust.Shared.Map;
using System.Numerics;

namespace Content.Shared.Examine
{
    /// <summary>
    /// Событие, вызываемое при осмотре сущности
    /// </summary>
    public sealed class EntityExaminedEvent : EntityEventArgs
    {
        /// <summary>
        /// Сущность, осматривающая объект
        /// </summary>
        public EntityUid Examiner { get; }

        /// <summary>
        /// Осматриваемая сущность
        /// </summary>
        public EntityUid Examined { get; }

        /// <summary>
        /// Позиция клика в мировых координатах
        /// </summary>
        public Vector2 ExamineLocation { get; }

        public EntityExaminedEvent(EntityUid examiner, EntityUid examined, Vector2 examineLocation)
        {
            Examiner = examiner;
            Examined = examined;
            ExamineLocation = examineLocation;
        }

        private readonly List<string> _message = new();

        public void PushText(string text)
        {
            _message.Add(text);
        }

        public void PushMarkup(string markup)
        {
            _message.Add(markup);
        }

        public string[] GetMessage()
        {
            return _message.ToArray();
        }
    }
}
