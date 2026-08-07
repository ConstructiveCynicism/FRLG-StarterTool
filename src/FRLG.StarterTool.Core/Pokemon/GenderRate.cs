namespace FRLG.StarterTool.Core.Pokemon;

public enum GenderRate
{
    MaleOnly = 0,
    Male87 = 1,
    Male75 = 2,
    Male50 = 3,
    Male25 = 4,
    FemaleOnly = 5,
    Genderless = 6
}

public static class GenderRateExtensions
{
    public static GenderRate FromInt(int n) => n switch
    {
        0 => GenderRate.MaleOnly,
        1 => GenderRate.Male87,
        2 => GenderRate.Male75,
        3 => GenderRate.Male50,
        4 => GenderRate.Male25,
        5 => GenderRate.FemaleOnly,
        6 => GenderRate.Genderless,
        _ => GenderRate.Genderless
    };

    public static string GetDisplayName(this GenderRate gr) => gr switch
    {
        GenderRate.MaleOnly => "Male only",
        GenderRate.FemaleOnly => "Female only",
        GenderRate.Genderless => "Genderless",
        GenderRate.Male25 => "Male 25%",
        GenderRate.Male50 => "Male 50%",
        GenderRate.Male75 => "Male 75%",
        GenderRate.Male87 => "Male 87.5%",
        _ => gr.ToString()
    };
}
