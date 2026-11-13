# Filmweb dla Jellyfin

Wtyczka dopasowuje filmy i seriale z biblioteki do [Filmweb.pl](https://www.filmweb.pl) i pokazuje polską ocenę użytkowników w Jellyfinie.

Ocena Filmwebu (skala 0–10, jedno miejsce po przecinku) trafia do **oceny społeczności**. To ta liczba przy plakacie i na stronie tytułu. Ocenę krytyków można opcjonalnie zapisać jako ocenę krytyków Jellyfina (Filmweb podaje ją w skali 0–10, a Jellyfin pokazuje procent, więc 7,9 staje się 79). Po dopasowaniu w linkach zewnętrznych pojawia się strona tytułu na Filmwebie, a w edytorze metadanych pole na identyfikator.

Filmweb nie udostępnia oficjalnego API dla tej wtyczki. Używany jest ten sam publiczny odczyt JSON, z którego korzysta strona. Może się zmienić bez zapowiedzi. Między zapytaniami jest odstęp, domyślnie 300 ms.

## Wymagania

Jellyfin **12.0** lub nowszy. Wtyczka jest zbudowana na .NET 10 i sprawdzona na Jellyfin 12.4. Do kompilacji potrzebny jest zestaw .NET SDK 10.

## Kompilacja

W katalogu repozytorium:

```powershell
dotnet test
dotnet publish src\Jellyfin.Plugin.Filmweb\Jellyfin.Plugin.Filmweb.csproj -c Release -o publish
```

Z katalogu `publish` skopiuj tylko:

- `Jellyfin.Plugin.Filmweb.dll`
- `meta.json`

Nie kopiuj bibliotek Jellyfina, jeśli pojawią się obok. Serwer ma je u siebie.

## Instalacja z katalogu

W panelu Jellyfina otwórz repozytoria wtyczek i wklej:

```
https://raw.githubusercontent.com/Balbiii/jellyfin-plugin-filmweb/main/manifest.json
```

Na stronie wtyczek kliknij **Dostępne** albo **Wszystkie**. Domyślny widok pokazuje tylko zainstalowane pozycje, więc nowej wtyczki tam nie ma. Zainstaluj **Filmweb** i zrestartuj serwer.

## Instalacja ręczna

1. Utwórz katalog wtyczki, na przykład:
   - Windows: `%LOCALAPPDATA%\jellyfin\plugins\Filmweb`
   - Docker: `/config/plugins/Filmweb` (katalog danych kontenera)
2. Włóż tam `Jellyfin.Plugin.Filmweb.dll` i `meta.json`.
3. Zrestartuj Jellyfin.
4. W panelu otwórz **Zaplanowane zadania** i uruchom **Aktualizuj oceny Filmweb**.

Sam restart nie przechodzi po biblioteki. Zadanie dzienne jest ustawione na 4:30. Pierwszy przebieg odpytuje Filmweb i przy dużej bibliotece trwa długo. Kolejne korzystają z pamięci podręcznej (domyślnie 7 dni) i kończą się szybko.

Nowy tytuł dostaje ocenę przy pierwszym odświeżeniu metadanych, a istniejąca biblioteka przy pełnym odświeżeniu albo przez zadanie. Filmweb nie pojawia się na liście pobierania metadanych biblioteki: wtyczka nie jest scraperem opisu, tylko dopisuje ocenę i identyfikator po innych źródłach. Nie podmienia obsady ani plakatów. Opis rusza tylko wtedy, gdy włączysz dopisek.

## Ustawienia

Panel → Wtyczki → Filmweb.

- **Ocena użytkowników** — domyślnie zastępuje ocenę społeczności. Można tylko uzupełniać puste pole albo nic nie zapisywać.
- **Ocena krytyków** — domyślnie wyłączona, żeby nie nadpisać Rotten Tomatoes.
- **Dopisek w opisie** — jedna linia `«Filmweb» 8,2/10 (liczba ocen)` na początku opisu. Wyłączenie kasuje ją przy następnym przebiegu.
- **Minimalna liczba głosów** — poniżej progu identyfikator jest zapamiętywany, ale ocena zostaje bez zmian.
- **Pamięć podręczna i odstęp zapytań** — jak często i jak grzecznie odpytywać Filmweb.

Zablokowany opis nie jest ruszany. Zablokowana pozycja jest pomijana.

Zastąpienie oceny społeczności nadpisuje liczbę z TMDb albo IMDb. Wtyczka nie pamięta poprzedniej wartości. Żeby wrócić do tamtej oceny, ustaw zapis oceny użytkowników na „Nie zapisuj” i odśwież metadane z zamianą.

## Dopasowanie

Kolejność:

1. Identyfikator Filmweb już zapisany przy pozycji.
2. Wyszukiwanie po tytule i tytule oryginalnym.
3. Porównanie polskiego i oryginalnego tytułu oraz roku (dopuszczalny rok w przód lub w tył). Dla serialu liczy się rok rozpoczęcia albo rok wewnątrz emisji.
4. Gdy zostaje więcej niż jeden równie dobry wynik, wtyczka nic nie zgaduje. W logu jest informacja „brak pewnego dopasowania”.

Identyfikator można wpisać ręcznie w edytorze metadanych, w polu Filmweb. To numer z adresu strony, na przykład `500891` w `https://www.filmweb.pl/film/Incepcja-2010-500891`.

Rok w nazwie pliku, na przykład `Incepcja (2010)`, jest używany, gdy Jellyfin nie ma roku produkcji.

## Czego wtyczka nie robi

Nie pobiera ocen odcinków ani sezonów. Na Filmwebie oceniany jest cały serial. Nie loguje się na konto i nie pokazuje twoich prywatnych ocen, tylko średnią użytkowników serwisu.
