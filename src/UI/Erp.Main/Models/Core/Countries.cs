namespace Erp.Main.Models.Core;

/// <summary>
/// Countries offered on partner/company address forms. Portugal first, since almost every
/// customer or supplier is domestic — not a full ISO 3166 list, just the countries this ERP's
/// customers actually trade with.
/// </summary>
public static class Countries
{
    public static readonly (string Code, string Name)[] All =
    [
        ("PT", "Portugal"),
        ("ES", "Espanha"),
        ("FR", "França"),
        ("DE", "Alemanha"),
        ("IT", "Itália"),
        ("GB", "Reino Unido"),
        ("IE", "Irlanda"),
        ("NL", "Países Baixos"),
        ("BE", "Bélgica"),
        ("LU", "Luxemburgo"),
        ("CH", "Suíça"),
        ("AT", "Áustria"),
        ("DK", "Dinamarca"),
        ("SE", "Suécia"),
        ("FI", "Finlândia"),
        ("NO", "Noruega"),
        ("PL", "Polónia"),
        ("CZ", "República Checa"),
        ("GR", "Grécia"),
        ("HU", "Hungria"),
        ("RO", "Roménia"),
        ("BG", "Bulgária"),
        ("HR", "Croácia"),
        ("SI", "Eslovénia"),
        ("SK", "Eslováquia"),
        ("EE", "Estónia"),
        ("LV", "Letónia"),
        ("LT", "Lituânia"),
        ("MT", "Malta"),
        ("CY", "Chipre"),
        ("US", "Estados Unidos"),
        ("CA", "Canadá"),
        ("BR", "Brasil"),
        ("AO", "Angola"),
        ("MZ", "Moçambique"),
        ("CV", "Cabo Verde"),
        ("GW", "Guiné-Bissau"),
        ("ST", "São Tomé e Príncipe"),
        ("TL", "Timor-Leste"),
        ("CN", "China"),
        ("IN", "Índia"),
        ("JP", "Japão"),
        ("AU", "Austrália"),
        ("ZA", "África do Sul"),
        ("MA", "Marrocos"),
        ("AE", "Emirados Árabes Unidos")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(country => country.Code == code).Name ?? code;
}
