using UnityEngine;

namespace ClustrGalaxy.Interaction
{
    public sealed class CommentThreadNode : MonoBehaviour
    {
        private GameObject threadVisual = null!;
        public int ReplyCount { get; private set; }

        public void Initialize(GameObject visual, int replyCount = 0)
        {
            threadVisual = visual;
            ReplyCount = Mathf.Max(0, replyCount);
            threadVisual.SetActive(false);
        }

        public void SetExpanded(bool expanded)
        {
            if (threadVisual != null)
            {
                threadVisual.SetActive(expanded);
            }
        }
    }
}
