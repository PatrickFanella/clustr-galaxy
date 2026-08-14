using ClustrGalaxy.UI;
using UnityEngine;

namespace ClustrGalaxy.Interaction
{
    public sealed class CommentThreadNode : MonoBehaviour
    {
        public long ReplyCount { get; private set; }
        public ThreadInspectionModel DemoModel { get; private set; } = null!;

        public void Initialize(long replyCount = 0, ThreadInspectionModel demoModel = null)
        {
            ReplyCount = System.Math.Max(0L, replyCount);
            DemoModel = demoModel;
        }
    }
}
