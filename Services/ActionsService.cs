using Scriptly.Models;

namespace Scriptly.Services;

public class ActionsService
{
    private readonly SettingsService _settingsService;

    private static readonly List<ActionItem> _builtInActions = new()
    {
        new ActionItem { Id = "fix_grammar",   Name = "Fix Grammar",     Icon = "✏️",  Shortcut = "G", Description = "Fix spelling and grammar mistakes",           IsBuiltIn = true, Prompt = "Fix the grammar and spelling of the following text. Return only the corrected text without any explanation:\n\n{text}" },
        new ActionItem { Id = "summarize",     Name = "Summarize",       Icon = "≡",   Shortcut = "S", Description = "Create a concise summary",                    IsBuiltIn = true, Prompt = "Summarize the following text concisely. Return only the summary:\n\n{text}" },
        new ActionItem { Id = "translate",     Name = "Translate",       Icon = "×",   Shortcut = "T", Description = "Translate to English or detect language",     IsBuiltIn = true, Prompt = "Translate the following text to English (if already English, translate to Spanish). Return only the translated text:\n\n{text}" },
        new ActionItem { Id = "expand",         Name = "Expand Text",     Icon = "↕",   Shortcut = "E", Description = "Elaborate and expand on the text",            IsBuiltIn = true, Prompt = "Expand and elaborate on the following text, adding more detail and depth. Return only the expanded text:\n\n{text}" },
        new ActionItem { Id = "shorten",        Name = "Shorten Text",    Icon = "↔",   Shortcut = "H", Description = "Make the text shorter and more concise",      IsBuiltIn = true, Prompt = "Shorten the following text while preserving its key meaning. Return only the shortened text:\n\n{text}" },
        new ActionItem { Id = "change_tone",    Name = "Change Tone",     Icon = "🎭",  Shortcut = "",  Description = "Make it professional and polished",            IsBuiltIn = true, Prompt = "Rewrite the following text in a professional, polished tone. Return only the rewritten text:\n\n{text}" },
        new ActionItem { Id = "rewrite",        Name = "Rewrite",         Icon = "↺",   Shortcut = "R", Description = "Rewrite in a cleaner, clearer way",            IsBuiltIn = true, Prompt = "Rewrite the following text to be clearer and more effective. Return only the rewritten text:\n\n{text}" },
        new ActionItem { Id = "explain",        Name = "Explain",         Icon = "💡",  Shortcut = "X", Description = "Explain what this text means",                IsBuiltIn = true, Prompt = "Explain the following text in simple terms. Return only the explanation:\n\n{text}" },
        new ActionItem { Id = "bullet_points",  Name = "Bullet Points",   Icon = "•",   Shortcut = "B", Description = "Convert to bullet points",                    IsBuiltIn = true, Prompt = "Convert the following text into clear bullet points. Return only the bullet points:\n\n{text}" },
        new ActionItem { Id = "casual_tone",    Name = "Casual Tone",     Icon = "😊",  Shortcut = "",  Description = "Make it casual and friendly",                 IsBuiltIn = true, Prompt = "Rewrite the following text in a casual, friendly tone. Return only the rewritten text:\n\n{text}" },
        new ActionItem { Id = "improve",        Name = "Improve Writing", Icon = "⬆",   Shortcut = "I", Description = "Improve clarity, flow, and impact",           IsBuiltIn = true, Prompt = "Improve the following text for better clarity, flow, and impact. Return only the improved text:\n\n{text}" },
        new ActionItem { Id = "explain_code",   Name = "Explain Code",    Icon = "🖥",  Shortcut = "C", Description = "Explain what this code does",                 IsBuiltIn = true, Prompt = "Explain what the following code does in simple terms. Return only the explanation:\n\n{text}" },
    };

    public ActionsService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public List<ActionItem> GetAllActions()
    {
        var settings = _settingsService.Load();
        var all = new List<ActionItem>(_builtInActions);

        foreach (var custom in settings.CustomActions)
        {
            all.Add(new ActionItem
            {
                Id = custom.Id.ToString(),
                Name = custom.Name,
                Description = custom.Description,
                Icon = custom.Icon,
                Shortcut = "",
                Prompt = custom.Instructions,
                IsBuiltIn = false
            });
        }

        return all;
    }

    public List<ActionItem> GetSmartSuggestions(string text)
    {
        var all = GetAllActions();

        // Smart detection ordering
        if (IsCode(text))
            return Reorder(all, "explain_code", "rewrite", "fix_grammar");

        if (IsEmail(text))
            return Reorder(all, "change_tone", "fix_grammar", "shorten", "improve");

        if (IsLongParagraph(text))
            return Reorder(all, "summarize", "bullet_points", "shorten", "improve");

        if (IsShortSentence(text))
            return Reorder(all, "fix_grammar", "rewrite", "improve", "expand");

        return all;
    }

    private static List<ActionItem> Reorder(List<ActionItem> all, params string[] firstIds)
    {
        var result = new List<ActionItem>();
        foreach (var id in firstIds)
        {
            var item = all.FirstOrDefault(a => a.Id == id);
            if (item != null) result.Add(item);
        }
        result.AddRange(all.Where(a => !firstIds.Contains(a.Id)));
        return result;
    }

    private static bool IsCode(string text)
    {
        var codeIndicators = new[] { "{", "}", "=>", "->", "def ", "func ", "class ", "import ", "using ", "var ", "const ", "let ", "function ", "#include", "public ", "private ", "return ", "if (", "for (", "while (" };
        return codeIndicators.Any(indicator => text.Contains(indicator));
    }

    private static bool IsEmail(string text)
    {
        var emailIndicators = new[] { "Dear ", "Hi ", "Hello ", "Best regards", "Thanks", "Sincerely", "Subject:" };
        return emailIndicators.Any(indicator => text.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLongParagraph(string text) => text.Length > 300 && text.Split('.').Length > 3;

    private static bool IsShortSentence(string text) => text.Length < 150;
}
