namespace TripPlanner.Web.Features.Timezones;

/// <summary>
/// Extra city names a traveler may search for, keyed by the canonical zone id.
/// Windows zone display names list only a couple of cities each, so searches for
/// "Seattle" or "Milan" would otherwise return nothing. Search-only; never displayed as the zone name.
/// </summary>
internal static class TimezoneSearchAliases
{
    private static readonly Dictionary<string, string[]> ByZoneId = new(StringComparer.Ordinal)
    {
        ["Pacific/Honolulu"] = ["Honolulu", "Waikiki", "Oahu", "Maui", "Kona", "Hilo", "Kauai"],
        ["America/Anchorage"] = ["Juneau", "Fairbanks", "Denali", "Ketchikan"],
        ["America/Los_Angeles"] =
        [
            "Seattle", "Portland", "San Francisco", "San Diego", "Las Vegas", "Sacramento", "San Jose",
            "Oakland", "Anaheim", "Palm Springs", "Napa", "Tacoma", "Spokane", "Vancouver", "Victoria", "Whistler"
        ],
        ["America/Denver"] =
        [
            "Salt Lake City", "Albuquerque", "Santa Fe", "Boise", "Boulder", "Colorado Springs", "Aspen",
            "Vail", "Jackson Hole", "Calgary", "Edmonton", "Banff"
        ],
        ["America/Phoenix"] = ["Tucson", "Scottsdale", "Sedona", "Flagstaff"],
        ["America/Chicago"] =
        [
            "Houston", "Dallas", "Austin", "San Antonio", "Fort Worth", "Minneapolis", "St. Paul",
            "New Orleans", "Nashville", "Memphis", "St. Louis", "Kansas City", "Milwaukee",
            "Oklahoma City", "Omaha", "Des Moines", "Madison", "Winnipeg"
        ],
        ["America/New_York"] =
        [
            "Boston", "Miami", "Philadelphia", "Washington", "Atlanta", "Orlando", "Tampa", "Charlotte",
            "Pittsburgh", "Detroit", "Cleveland", "Baltimore", "Raleigh", "Cincinnati", "Columbus",
            "Buffalo", "Providence", "Richmond", "Jacksonville", "Savannah", "Charleston", "Key West",
            "Toronto", "Ottawa", "Montreal", "Quebec", "Niagara Falls"
        ],
        ["America/Halifax"] = ["Halifax", "Moncton", "Charlottetown"],
        ["America/Cancun"] = ["Cancun", "Playa del Carmen", "Tulum", "Cozumel", "Riviera Maya"],
        ["America/Mexico_City"] = ["Oaxaca", "Merida", "Puebla", "Guanajuato", "San Miguel de Allende"],
        ["America/Bogota"] = ["Medellin", "Cartagena", "Cusco", "Machu Picchu"],
        ["America/Sao_Paulo"] = ["Rio de Janeiro", "Rio", "Sao Paulo"],
        ["Atlantic/Reykjavik"] = ["Iceland", "Keflavik"],
        ["Europe/London"] =
        [
            "Manchester", "Liverpool", "Birmingham", "Glasgow", "Belfast", "Cardiff", "Bristol",
            "Oxford", "Cambridge", "Bath", "York", "Porto", "Madeira", "Galway"
        ],
        ["Europe/Paris"] =
        [
            "Barcelona", "Seville", "Valencia", "Bilbao", "San Sebastian", "Nice", "Lyon", "Marseille",
            "Bordeaux", "Cannes", "Normandy", "Bruges", "Antwerp", "Luxembourg"
        ],
        ["Europe/Berlin"] =
        [
            "Munich", "Frankfurt", "Hamburg", "Cologne", "Dusseldorf", "Zurich", "Geneva", "Lucerne",
            "Interlaken", "Salzburg", "Innsbruck", "Milan", "Florence", "Venice", "Naples", "Turin",
            "Verona", "Bologna", "Pisa", "Tuscany", "Amalfi", "Oslo", "Bergen", "Gothenburg", "Malta"
        ],
        ["Europe/Budapest"] = ["Prague", "Vienna", "Dubrovnik", "Split"],
        ["Europe/Warsaw"] = ["Krakow", "Gdansk", "Wroclaw"],
        ["Europe/Bucharest"] = ["Santorini", "Mykonos", "Crete", "Thessaloniki", "Rhodes", "Corfu"],
        ["Europe/Istanbul"] = ["Cappadocia", "Antalya", "Izmir", "Bodrum"],
        ["Africa/Casablanca"] = ["Marrakech", "Marrakesh", "Fes", "Rabat", "Tangier"],
        ["Africa/Cairo"] = ["Luxor", "Giza", "Aswan", "Sharm el Sheikh", "Hurghada"],
        ["Africa/Johannesburg"] = ["Cape Town", "Durban", "Kruger", "Stellenbosch"],
        ["Asia/Calcutta"] = ["Delhi", "Bangalore", "Bengaluru", "Hyderabad", "Goa", "Jaipur", "Agra", "Kochi"],
        ["Asia/Bangkok"] = ["Phuket", "Chiang Mai", "Krabi", "Ho Chi Minh", "Saigon", "Da Nang", "Hoi An", "Siem Reap"],
        ["Asia/Singapore"] = ["Penang", "Langkawi", "Borneo"],
        ["Asia/Shanghai"] = ["Shanghai", "Macau", "Shenzhen", "Guangzhou", "Xi'an", "Chengdu"],
        ["Asia/Seoul"] = ["Busan", "Jeju", "Incheon"],
        ["Asia/Tokyo"] = ["Kyoto", "Hiroshima", "Nagoya", "Fukuoka", "Okinawa", "Nara", "Hakone", "Kanazawa", "Niseko"],
        ["Australia/Sydney"] = ["Hunter Valley", "Blue Mountains"],
        ["Australia/Brisbane"] = ["Gold Coast", "Cairns", "Whitsundays", "Great Barrier Reef"],
        ["Australia/Perth"] = ["Margaret River"],
        ["Pacific/Auckland"] = ["Christchurch", "Queenstown", "Rotorua", "Dunedin"],
        ["Pacific/Fiji"] = ["Nadi", "Denarau"],
    };

    public static IReadOnlyList<string> For(string zoneId) => ByZoneId.GetValueOrDefault(zoneId, []);
}
