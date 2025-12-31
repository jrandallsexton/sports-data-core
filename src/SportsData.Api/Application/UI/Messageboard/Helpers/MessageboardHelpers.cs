using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Messageboard.Helpers;

public static class MessageboardHelpers
{
    public static void AdjustReactionCounts(MessagePost post, ReactionType type, bool decrement)
    {
        int delta = decrement ? -1 : 1;
        
        switch (type)
        {
            case ReactionType.Like:
                post.LikeCount += delta;
                post.LikeCount = Math.Max(0, post.LikeCount);
                break;
                
            case ReactionType.Dislike:
                post.DislikeCount += delta;
                post.DislikeCount = Math.Max(0, post.DislikeCount);
                break;
                
            case ReactionType.Laugh:
                post.LikeCount += delta;
                post.LikeCount = Math.Max(0, post.LikeCount);
                break;
                
            case ReactionType.Sad:
                post.DislikeCount += delta;
                post.DislikeCount = Math.Max(0, post.DislikeCount);
                break;
                
            case ReactionType.Angry:
                post.DislikeCount += delta;
                post.DislikeCount = Math.Max(0, post.DislikeCount);
                break;
                
            case ReactionType.Surprise:
                post.LikeCount += delta;
                post.LikeCount = Math.Max(0, post.LikeCount);
                break;
                
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown reaction type");
        }
    }

    public static string ToFixedBase36(int value, int width)
    {
        // Validate inputs - fail fast
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Value must be >= 0.");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                "Width must be > 0.");
        }

        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var stack = new Stack<char>();
        
        // Use the original value after validation (no Math.Max coercion)
        int v = value;
        do
        {
            stack.Push(alphabet[v % 36]);
            v /= 36;
        } while (v > 0);

        var s = new string(stack.ToArray());
        return s.PadLeft(width, '0');
    }
}
