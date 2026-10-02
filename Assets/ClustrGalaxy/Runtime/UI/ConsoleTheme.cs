using UnityEngine;
using UnityEngine.UI;
namespace ClustrGalaxy.UI
{
    public static class ConsoleTheme
    {
        public static void Style(Button button)
        {
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.64f,.82f,.87f);colors.selectedColor=new Color(1f,.76f,.4f);colors.pressedColor=new Color(.8f,.49f,.17f);colors.fadeDuration=.08f;button.colors=colors;
            if(button.GetComponent<Outline>()==null){var outline=button.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.32f,.48f,.53f,.65f);outline.effectDistance=new Vector2(1,-1);}
        }
    }
}
