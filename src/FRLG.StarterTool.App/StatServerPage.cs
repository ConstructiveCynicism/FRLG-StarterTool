using System.Globalization;

namespace FRLG.StarterTool.App;

public static class StatServerPage
{
    public const string LayoutRow = "row";

    public const string LayoutColumn = "column";

    public const string BoxBoth = "both";

    public const string BoxIvs = "ivs";

    public const string BoxStats = "stats";

    public static string Build(string prefix, int scale, string layout, string box)
    {
        int gap = 2 * scale;

        return Template
            .Replace("__PREFIX__", Escape(prefix))
            .Replace("__SCALE__", scale.ToString(CultureInfo.InvariantCulture))
            .Replace("__GAP__", gap.ToString(CultureInfo.InvariantCulture))
            .Replace("__LAYOUT__", layout == LayoutColumn ? LayoutColumn : LayoutRow)
            .Replace("__BOX__", box);
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("<", "\\u003c");

    private const string Template =
        """
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <title>Capture boxes</title>
        <style>
        html, body { margin: 0; padding: 0; background: transparent; }
        #boxes { display: flex; flex-direction: row; gap: __GAP__px; align-items: flex-start; }
        #boxes.column { flex-direction: column; }
        #boxes img { display: block; }
        #boxes.ivs #stats { display: none; }
        #boxes.stats #ivs { display: none; }
        #boxes #post { display: none; }
        #boxes.card #ivs, #boxes.card #stats { display: none; }
        #boxes.card #post { display: block; }
        #boxes.ivs #post, #boxes.stats #post { width: 100%; height: auto; }
        #boxes.ivs, #boxes.stats { width: 100%; }
        #boxes.ivs img, #boxes.stats img { width: 100%; height: auto; }
        </style>
        </head>
        <body>
        <div id="boxes" class="__LAYOUT__ __BOX__">
        <img id="ivs" alt="">
        <img id="stats" alt="">
        <img id="post" alt="">
        </div>
        <script>
        var base = "__PREFIX__";
        var scale = "__SCALE__";
        var box = "__BOX__";
        var boxes = document.getElementById("boxes");
        var ivs = document.getElementById("ivs");
        var stats = document.getElementById("stats");
        var post = document.getElementById("post");
        function show(version, card) {
          var tail = ".png?scale=" + scale + "&v=" + version;
          if (card) {
            post.src = base + "post" + tail + "&box=" + box;
            boxes.classList.add("card");
            return;
          }
          boxes.classList.remove("card");
          if (box !== "stats") { ivs.src = base + "ivs" + tail; }
          if (box !== "ivs") { stats.src = base + "stats" + tail; }
        }
        show(Date.now(), false);
        var events = new EventSource(base + "events");
        events.onmessage = function (event) {
          var parts = String(event.data).split(":");
          show(parts[0], parts[1] === "1");
        };
        </script>
        </body>
        </html>
        """;
}
