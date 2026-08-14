using System;
using System.Text;

namespace ClustrGalaxy.Generation
{
    public static class CommentRingLayout
    {
        private const double Tau = Math.PI * 2d;

        public static double AngleRadians(string stableCommentId)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            var bytes = Encoding.UTF8.GetBytes(stableCommentId ?? string.Empty);
            for (var index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= prime;
            }

            return (hash >> 11) * (1d / 9007199254740992d) * Tau;
        }
    }
}
