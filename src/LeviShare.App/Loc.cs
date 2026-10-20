using System.Globalization;

namespace LeviShare.App;

/// <summary>UI strings. Bound as <c>L[Key]</c>; a new instance is created when the language changes.</summary>
public sealed class Loc(bool japanese)
{
    public bool Japanese { get; } = japanese;

    public static bool SystemIsJapanese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";

    public string this[string key] => (Japanese ? Ja : En).TryGetValue(key, out var v) ? v : En.GetValueOrDefault(key, key);

    public string Format(string key, params object[] args) => string.Format(this[key], args);

    static readonly Dictionary<string, string> En = new()
    {
        ["Subtitle"] = "Share a LeviLauncher setup (mods and packs) as a zip that LeviLauncher can restore.",
        ["DataFolder"] = "LeviLauncher data",
        ["Change"] = "Change...",
        ["Reload"] = "Reload",
        ["Instance"] = "Instance",
        ["NoData"] = "LeviLauncher's data folder was not found. Use \"Change...\" to pick the folder that contains \"versions\".",
        ["NoInstances"] = "No instances found in this folder.",
        ["UwpInstance"] = "UWP instances cannot load mods and are not supported.",
        ["InstanceNotFound"] = "The instance could not be read.",
        ["SharedData"] = "Packs are read from the game data shared with the game itself (version isolation is off).",
        ["LipTitle"] = "Mods installed with lip",
        ["LipNote"] = "Only the name and version are shared. LeviLauncher installs them again with lip when restoring.",
        ["RawTitle"] = "Mods added by hand",
        ["RawNote"] = "Shared as files. Logs, debug symbols (.pdb) and per-PC IDs are left out.",
        ["ResourceTitle"] = "Resource packs",
        ["BehaviorTitle"] = "Behavior packs",
        ["SkinTitle"] = "Skin packs",
        ["Disabled"] = "disabled",
        ["Dependency"] = "dependency",
        ["Encrypted"] = "Marketplace packs are never shared: {0}",
        ["None"] = "(none)",
        ["SelectAll"] = "All",
        ["SelectNone"] = "None",
        ["Summary"] = "{0} lip mods, {1} mods, {2} packs  ·  {3} files, {4}",
        ["SummaryCounting"] = "{0} lip mods, {1} mods, {2} packs  ·  counting files...",
        ["Excluded"] = "{0} files left out (logs, .pdb, IDs)",
        ["Repaired"] = "{0} pack manifests cleaned up for LeviLauncher (content unchanged)",
        ["Export"] = "Export share zip...",
        ["Exporting"] = "Exporting... large setups can take a minute.",
        ["SaveTitle"] = "Save share zip",
        ["Exported"] = "Saved: {0}",
        ["OpenFolder"] = "Open folder",
        ["CopySteps"] = "Copy steps for the recipient",
        ["Copied"] = "Copied to the clipboard.",
        ["NothingSelected"] = "Select at least one mod or pack.",
        ["Error"] = "Error: {0}",
        ["HowToImport"] = "How the recipient imports it",
        ["ImportSteps"] =
            "1. In LeviLauncher, open Settings > Others and turn on \"Instance backup (experimental)\".\n" +
            "2. Download Minecraft {0} and create an instance.\n" +
            "3. Open that instance's settings > Manage > \"Restore backup\" and choose the zip.\n" +
            "4. Mods installed with lip are installed again automatically (lip must be installed: Settings > Third-party components).\n" +
            "5. Resource packs are copied but not switched on: turn them on in Minecraft (Settings > Global resources).",
        ["About"] = "LeviShare {0} · MIT License · not affiliated with LiteLDev or Mojang",
    };

    static readonly Dictionary<string, string> Ja = new()
    {
        ["Subtitle"] = "LeviLauncher の構成（Mod とパック）を、LeviLauncher で復元できる zip にして共有します。",
        ["DataFolder"] = "LeviLauncher のデータ",
        ["Change"] = "変更...",
        ["Reload"] = "再読み込み",
        ["Instance"] = "インスタンス",
        ["NoData"] = "LeviLauncher のデータフォルダが見つかりません。「変更...」から versions フォルダを含むフォルダを選んでください。",
        ["NoInstances"] = "このフォルダにはインスタンスがありません。",
        ["UwpInstance"] = "UWP 版のインスタンスは Mod を読み込めないため対応していません。",
        ["InstanceNotFound"] = "インスタンスを読み込めませんでした。",
        ["SharedData"] = "バージョン分離がオフのため、ゲーム本体と共有のデータからパックを読んでいます。",
        ["LipTitle"] = "lip で入れた Mod",
        ["LipNote"] = "名前とバージョンだけを共有します。復元時に LeviLauncher が lip で入れ直します。",
        ["RawTitle"] = "手動で入れた Mod",
        ["RawNote"] = "ファイルごと共有します。ログ・デバッグ情報（.pdb）・PC 固有の ID は除きます。",
        ["ResourceTitle"] = "リソースパック",
        ["BehaviorTitle"] = "ビヘイビアパック",
        ["SkinTitle"] = "スキンパック",
        ["Disabled"] = "無効",
        ["Dependency"] = "依存",
        ["Encrypted"] = "マーケットプレイスのパックは共有できません: {0}",
        ["None"] = "（なし）",
        ["SelectAll"] = "すべて",
        ["SelectNone"] = "なし",
        ["Summary"] = "lip Mod {0} 件・Mod {1} 件・パック {2} 件  ·  {3} ファイル、{4}",
        ["SummaryCounting"] = "lip Mod {0} 件・Mod {1} 件・パック {2} 件  ·  ファイルを数えています…",
        ["Excluded"] = "{0} 個のファイルを除外（ログ・.pdb・ID）",
        ["Repaired"] = "{0} 個のパックの manifest を LeviLauncher 用に整形（内容は同じ）",
        ["Export"] = "共有用 zip を書き出す...",
        ["Exporting"] = "書き出し中…（大きい構成では 1 分ほどかかります）",
        ["SaveTitle"] = "共有用 zip の保存先",
        ["Exported"] = "保存しました: {0}",
        ["OpenFolder"] = "フォルダを開く",
        ["CopySteps"] = "受け取る人への手順をコピー",
        ["Copied"] = "クリップボードにコピーしました。",
        ["NothingSelected"] = "Mod かパックを 1 つ以上選んでください。",
        ["Error"] = "エラー: {0}",
        ["HowToImport"] = "受け取った人の取り込み方",
        ["ImportSteps"] =
            "1. LeviLauncher の 設定 > その他 で「インスタンスバックアップ（実験的）」をオンにする。\n" +
            "2. Minecraft {0} をダウンロードしてインスタンスを作る。\n" +
            "3. そのインスタンスの設定 > 管理 >「バックアップを復元」で zip を選ぶ。\n" +
            "4. lip で入れる Mod は自動で入ります（lip が必要: 設定 > サードパーティ製コンポーネント）。\n" +
            "5. リソースパックは入りますが有効にはならないので、Minecraft の 設定 > グローバルリソース で有効にする。",
        ["About"] = "LeviShare {0} · MIT ライセンス · LiteLDev・Mojang とは無関係の非公式ツール",
    };
}
