using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BadNorthAPI
{
    public static class CustomSprites
    {
        public static void AddCustomSprite(string path, string name)
        {
            try
            {
                Texture2D texture2D = new Texture2D(200, 200);
                ImageConversion.LoadImage(texture2D, File.ReadAllBytes(path + Path.DirectorySeparatorChar.ToString() + name + ".png"));
                texture2D.filterMode = FilterMode.Bilinear;
                texture2D.wrapMode = TextureWrapMode.Clamp;
                CustomSprites.Sprites[name] = Sprite.Create(texture2D, new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), new Vector2((float)(texture2D.width / 2), (float)(texture2D.height / 2)));
                Plugin.logger.LogInfo("Added custom sprite with id " + name + "!");
            }
            catch (Exception ex)
            {
                Plugin.logger.LogError("Failed to add sprite " + path + "\\" + name);
                Plugin.logger.LogError(ex);
            }
        }

        public static Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    }
}
