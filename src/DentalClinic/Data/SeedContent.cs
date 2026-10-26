using DentalClinic.Models;

namespace DentalClinic.Data;

/// <summary>
/// Početni sadržaj sajta: usluge, doktori i radno vrijeme. Svi podaci su izmišljeni (demo ordinacija),
/// a admin ih poslije može mijenjati u panelu.
/// </summary>
public static class SeedContent
{
    public static void AddServicesAndDoctors(ClinicDb db)
    {
        var services = new List<Service>
        {
            new()
            {
                Slug = "pregled", Name = "Pregled i savjetovanje", Category = "Opća stomatologija", SortOrder = 1,
                Summary = "Detaljan pregled zuba i desni, plan liječenja i cijena unaprijed, bez iznenađenja.",
                Description = """
                    Pregled je prvi korak i najbolja investicija u zdravlje zuba. Doktor pregleda sve zube, desni, jezik i sluznicu, a po potrebi napravi i rendgenski snimak.

                    Nakon pregleda dobijate jasan plan: šta treba odmah, šta može sačekati i koliko će koštati. Odluku donosite vi, bez ikakvog pritiska.
                    """,
                Steps = """
                    Razgovor o vašim tegobama i zdravstvenoj historiji
                    Pregled zuba, desni i sluznice
                    Rendgenski snimak ako je potreban
                    Objašnjenje nalaza na ekranu, na razumljiv način
                    Pisani plan liječenja s cijenom
                    """,
                Aftercare = """
                    Nema posebnih uputa nakon pregleda
                    Ako ste dobili snimak, možete ga zatražiti i u digitalnom obliku
                    """,
                SuitableFor = "Svi, a posebno novi pacijenti, osobe koje dugo nisu bile kod zubara i svako ko osjeća bol ili osjetljivost.",
                DurationMinutes = 30, PriceFrom = 30, Comfort = "Bezbolno", ImageName = "exam",
            },
            new()
            {
                Slug = "ciscenje-kamenca", Name = "Čišćenje kamenca i poliranje", Category = "Preventiva", SortOrder = 2,
                Summary = "Uklanjanje kamenca i naslaga ultrazvukom, poliranje i savjeti za svakodnevnu njegu.",
                Description = """
                    Kamenac je očvrsnuli plak koji se ne može ukloniti četkicom. Iritira desni, izaziva krvarenje i neprijatan zadah, a dugoročno vodi do paradentoze i gubitka zuba.

                    Čišćenje ultrazvukom je brzo i ugodno. Nakon njega slijedi poliranje koje zubima vraća glatku površinu, pa se plak sporije lijepi.
                    """,
                Steps = """
                    Pregled stanja desni
                    Uklanjanje kamenca ultrazvučnim aparatom
                    Uklanjanje taloga od kafe, čaja i cigareta pjeskarenjem
                    Poliranje zuba pastom
                    Nanošenje fluorida protiv osjetljivosti
                    Savjet o tehnici pranja i odgovarajućoj četkici
                    """,
                Aftercare = """
                    Dva sata ne jedite i ne pijte
                    Dan-dva izbjegavajte jako obojena pića (kafa, crno vino, čaj)
                    Blago krvarenje desni prvog dana je normalno i brzo prolazi
                    """,
                SuitableFor = "Svi odrasli, preporuka je dva puta godišnje. Posebno pušači, ljubitelji kafe i osobe čije desni krvare.",
                DurationMinutes = 60, PriceFrom = 70, PriceTo = 90, Comfort = "Bezbolno, blago škakljanje", ImageName = "cleaning",
            },
            new()
            {
                Slug = "plombe", Name = "Plombe (estetsko punjenje)", Category = "Opća stomatologija", SortOrder = 3,
                Summary = "Liječenje karijesa kompozitnim plombama u boji vašeg zuba.",
                Description = """
                    Karijes je rupa u zubu koju izazivaju bakterije. Što se ranije liječi, to je zahvat manji, jeftiniji i bezbolniji.

                    Koristimo kompozitni materijal u boji vašeg zuba, pa se plomba ne vidi. Materijal se stvrdnjava posebnim svjetlom, a nakon zahvata možete odmah jesti.
                    """,
                Steps = """
                    Lokalna anestezija, a prije nje anestetički gel da ne osjetite ni ubod
                    Uklanjanje oboljelog dijela zuba
                    Pripremanje i zaštita zuba
                    Nanošenje kompozita u slojevima i stvrdnjavanje svjetlom
                    Oblikovanje i poliranje plombe
                    Provjera zagriza
                    """,
                Aftercare = """
                    Dok ne prođe utrnulost, pazite da ne ugrizete usnu ili obraz
                    Blaga osjetljivost nekoliko dana je normalna
                    Ako zagriz ne djeluje pravilno, javite se na korekciju, to je kratko i besplatno
                    """,
                SuitableFor = "Svako ko ima karijes, pukotinu ili izlomljen dio zuba.",
                DurationMinutes = 60, PriceFrom = 70, PriceTo = 120, Comfort = "Lokalna anestezija", ImageName = "tools",
            },
            new()
            {
                Slug = "lijecenje-kanala", Name = "Liječenje korijenskih kanala", Category = "Endodoncija", SortOrder = 4,
                Summary = "Spašavamo zub koji boli ili je jako oštećen, bez vađenja, uz modernu tehniku i mikroskop.",
                Description = """
                    Kada bakterije dođu do nerva zuba, javlja se jaka bol i upala. Liječenje kanala uklanja upaljeno tkivo, čisti kanale i zatvara ih, pa zub možete zadržati.

                    Mnogi se plaše ovog zahvata zbog priča iz prošlosti. Danas je on uz dobru anesteziju i savremene instrumente praktično jednako ugodan kao obična plomba.
                    """,
                Steps = """
                    Rendgenski snimak i dijagnoza
                    Potpuna anestezija zuba
                    Otvaranje zuba i uklanjanje upaljenog nerva
                    Mjerenje dužine, čišćenje i dezinfekcija kanala
                    Trajno zatvaranje kanala
                    Privremena ili trajna plomba, a često je potrebna i krunica
                    """,
                Aftercare = """
                    Blaga bol nekoliko dana je normalna, pomažu uobičajeni analgetici
                    Izbjegavajte žvakanje na tom zubu dok se ne završi cijeli tretman
                    Dolazite na dogovorene kontrole
                    """,
                SuitableFor = "Zub koji pulsirajuće boli, reaguje na toplo i hladno ili je jako oštećen karijesom.",
                DurationMinutes = 90, PriceFrom = 200, PriceTo = 350, Comfort = "Potpuna anestezija", ImageName = "xray",
            },
            new()
            {
                Slug = "izbjeljivanje", Name = "Izbjeljivanje zuba", Category = "Estetska stomatologija", SortOrder = 5,
                Summary = "Svjetliji osmijeh za sat i po. Sigurno, kontrolisano i uz zaštitu desni.",
                Description = """
                    Zubi s godinama tamne zbog kafe, čaja, vina, pušenja i prirodnog starenja. Profesionalno izbjeljivanje ih osvijetli za nekoliko tonova, što kućna sredstva iz drogerije ne mogu postići.

                    Prije izbjeljivanja uvijek radimo pregled i čišćenje kamenca. Desni štitimo posebnom barijerom, a gel nanosi doktor, pa je postupak siguran.
                    """,
                Steps = """
                    Pregled i čišćenje kamenca
                    Određivanje početne boje zuba
                    Zaštita desni i usana
                    Nanošenje gela i aktivacija svjetlom, u više ciklusa
                    Nanošenje fluorida protiv osjetljivosti
                    Kontrola boje i upute za održavanje
                    """,
                Aftercare = """
                    Prva 48 sati izbjegavajte kafu, čaj, crno vino, cigarete i jako obojenu hranu (tzv. bijela dijeta)
                    Osjetljivost zuba dan-dva je moguća i prolazi sama
                    Efekat traje 1 do 3 godine, a produžite ga dobrom higijenom
                    """,
                SuitableFor = "Zdravi zubi i desni. Ne radi se kod trudnica, dojilja i djece. Plombe i krunice se ne izbjeljuju, pa o tome razgovaramo unaprijed.",
                DurationMinutes = 90, PriceFrom = 350, Comfort = "Bezbolno, moguća osjetljivost", ImageName = "smile-1",
            },
            new()
            {
                Slug = "implantati", Name = "Zubni implantati: konzultacija", Category = "Oralna hirurgija", SortOrder = 6,
                Summary = "Trajno rješenje za zub koji nedostaje. Konzultacija sa snimkom i planom, a ugradnja kreće od 1.200 KM.",
                Description = """
                    Implantat je mali titanijski vijak koji se ugrađuje u kost i zamjenjuje korijen izgubljenog zuba. Na njega se stavlja krunica, pa dobijete zub koji se ne razlikuje od prirodnog.

                    Za razliku od mosta, implantat ne traži brušenje susjednih zuba i čuva kost vilice. Prvi korak je konzultacija sa snimkom, na kojoj dobijate plan i tačnu cijenu.

                    Konzultacija košta 40 KM i uračunava se u cijenu ako nastavite liječenje. Ugradnja implantata s krunicom kreće od oko 1.200 KM po zubu.
                    """,
                Steps = """
                    Konzultacija, 3D snimak i plan terapije
                    Ugradnja implantata uz lokalnu anesteziju (zahvat traje oko 30 do 60 minuta)
                    Period srastanja s kosti, obično 3 do 4 mjeseca
                    Postavljanje nadogradnje i izrada krunice
                    Redovne kontrole
                    """,
                Aftercare = """
                    Prva dva dana hladni oblozi i meka hrana
                    Antibiotik i analgetik po uputi doktora
                    Ne pušiti barem sedam dana: pušenje je glavni uzrok neuspjeha
                    Pažljivo pranje i redovno čišćenje kamenca doživotno
                    """,
                SuitableFor = "Odrasli kojima nedostaje jedan ili više zuba, a imaju dovoljno kosti i zdrave desni. Termin koji ovdje zakazujete je konzultacija.",
                DurationMinutes = 60, PriceFrom = 40, Comfort = "Konzultacija bez bola", ImageName = "implant",
            },
            new()
            {
                Slug = "krunice-mostovi", Name = "Krunice i mostovi", Category = "Protetika", SortOrder = 7,
                Summary = "Cirkonijske i keramičke krunice koje vraćaju oblik, snagu i izgled oštećenom zubu.",
                Description = """
                    Krunica je kapica koja prekriva i štiti jako oštećen zub, na primjer nakon liječenja kanala. Most nadoknađuje zub koji nedostaje tako što se oslanja na susjedne zube.

                    Radimo cirkonijske i metal-keramičke radove prema otisku ili digitalnom skeniranju. Boju birate zajedno s doktorom, tako da se rad ne razlikuje od ostalih zuba.
                    """,
                Steps = """
                    Pregled, snimak i dogovor o vrsti rada i boji
                    Brušenje zuba uz lokalnu anesteziju
                    Digitalni otisak ili skeniranje
                    Privremena krunica dok se rad izrađuje (oko 5 do 7 dana)
                    Probanje i trajno lijepljenje krunice
                    """,
                Aftercare = """
                    Dok nosite privremenu krunicu, izbjegavajte ljepljivu i tvrdu hranu
                    Higijena zubnim koncem ili interdentalnim četkicama
                    Kontrola jednom godišnje
                    """,
                SuitableFor = "Zubi sa velikim plombama, nakon liječenja kanala, napuknuti zubi i nedostajući zubi.",
                DurationMinutes = 60, PriceFrom = 400, PriceTo = 650, Comfort = "Lokalna anestezija", ImageName = "veneers",
            },
            new()
            {
                Slug = "ortodoncija", Name = "Ortodoncija: konzultacija", Category = "Ortodoncija", SortOrder = 8,
                Summary = "Ravni zubi s fiksnim aparatićem ili prozirnim folijama. Konzultacija s planom, a terapija kreće od 1.800 KM.",
                Description = """
                    Ortodontska terapija ne ispravlja samo izgled. Ravni zubi se lakše čiste, a ispravan zagriz rasterećuje čeljust i zube. Moguće je u svakoj dobi, ne samo u djetinjstvu.

                    Na prvoj konzultaciji ortodont analizira zagriz, snimi zube i objasni koja opcija je najbolja za vas: klasični fiksni aparatić ili prozirne folije koje skidate za jelo.

                    Konzultacija košta 40 KM i uračunava se ako počnete terapiju. Cijena cijele terapije zavisi od slučaja i kreće od oko 1.800 KM.
                    """,
                Steps = """
                    Konzultacija, fotografije i snimak
                    Plan terapije, trajanje i cijena unaprijed
                    Postavljanje aparatića ili prve serije folija
                    Kontrole na 4 do 8 sedmica
                    Faza zadržavanja: retainer da zubi ostanu na mjestu
                    """,
                Aftercare = """
                    Pažljivo pranje zuba nakon svakog obroka
                    Kod aparatića izbjegavajte ljepljivu i tvrdu hranu
                    Folije nosite 22 sata dnevno
                    Retainer nosite prema uputi, jer inače zubi pokušavaju da se vrate
                    """,
                SuitableFor = "Djeca od oko 7 godina (prvi pregled), tinejdžeri i odrasli s krivim zubima ili neispravnim zagrizom. Termin koji zakazujete je konzultacija.",
                DurationMinutes = 60, PriceFrom = 40, Comfort = "Konzultacija bez bola", ImageName = "braces-girl",
            },
            new()
            {
                Slug = "djecja-stomatologija", Name = "Dječja stomatologija", Category = "Djeca", SortOrder = 9,
                Summary = "Blag, strpljiv pristup djeci: pregled, zaštitni premaz i prvo prijateljstvo sa zubarom.",
                Description = """
                    Mliječni zubi su važni: čuvaju mjesto za stalne zube i uče dijete da jede i govori. Zato ih liječimo isto ozbiljno kao stalne.

                    Dolazak kod zubara ne treba biti stres. Doktor se djetetu prvo predstavi, pokaže mu "šta radi stolica", a tek onda počinje pregled. Roditelj je uvijek uz dijete.
                    """,
                Steps = """
                    Upoznavanje i igra s instrumentima, bez žurbe
                    Pregled zuba i procjena rizika od karijesa
                    Zaštitni premaz fluorida ili zalijevanje žlijebova na stalnim kutnjacima
                    Savjet roditeljima o ishrani i pranju
                    Nagrada za hrabrost
                    """,
                Aftercare = """
                    Ako je dijete dobilo fluorid, pola sata bez jela i pića
                    Pohvalite dijete: prvi utisak određuje kako će doživljavati zubare cijeli život
                    Kontrola svakih šest mjeseci
                    """,
                SuitableFor = "Djeca od 2 godine (prvi pregled kad izbiju prvi zubi) do 14 godina.",
                DurationMinutes = 30, PriceFrom = 25, Comfort = "Prilagođeno djeci", ImageName = "kids",
            },
            new()
            {
                Slug = "vadjenje-zuba", Name = "Vađenje zuba", Category = "Oralna hirurgija", SortOrder = 10,
                Summary = "Blago i precizno vađenje zuba uz potpunu anesteziju, uključujući umnjake.",
                Description = """
                    Zub se vadi tek kad ga nije moguće spasiti: kad je pukao ispod desni, jako oštećen ili kad umnjak pravi probleme. Prvo uvijek razmotrimo može li se zub liječiti.

                    Uz dobru anesteziju ne osjećate bol, samo pritisak. Doktor vam unaprijed objasni šta će raditi, a nakon zahvata dobijate pisane upute za oporavak.
                    """,
                Steps = """
                    Pregled i snimak
                    Lokalna anestezija
                    Vađenje zuba, kod umnjaka eventualno uz mali rez
                    Zbrinjavanje rane, po potrebi šav
                    Pisane upute za oporavak
                    """,
                Aftercare = """
                    Gazu držite zagrizenu 30 minuta
                    Prvih 24 sata ne ispirati usta i ne pušiti
                    Hladni oblozi prvog dana, meka i mlaka hrana
                    Javite se odmah ako je krvarenje jako ili bol raste poslije trećeg dana
                    """,
                SuitableFor = "Zubi koji se ne mogu spasiti, umnjaci koji rastu krivo i mliječni zubi koji se ne ljuljaju sami.",
                DurationMinutes = 60, PriceFrom = 80, PriceTo = 250, Comfort = "Potpuna anestezija", ImageName = "extraction",
            },
            new()
            {
                Slug = "hitni-slucaj", Name = "Hitni slučaj (bol, otok, slomljen zub)", Category = "Hitna pomoć", SortOrder = 11,
                Summary = "Jaka bol ili povreda? Primamo hitne slučajeve istog dana, čim je moguće.",
                Description = """
                    Zubna bol rijetko prolazi sama, a čekanje je obično čini jačom i skupljom za liječenje. Ako imate jaku bol, otok lica, izbijen ili slomljen zub, javite se odmah.

                    Doktor će utvrditi uzrok, ukloniti bol i dogovoriti nastavak liječenja. Ako ovdje nema slobodnih termina, nazovite nas, pa ćemo pokušati naći mjesto.
                    """,
                Steps = """
                    Kratak razgovor o tegobama
                    Pregled i brzi snimak
                    Otklanjanje bola, najčešće anestezijom i otvaranjem zuba
                    Plan daljnjeg liječenja
                    """,
                Aftercare = """
                    Pridržavajte se uputa o lijekovima
                    Ako je zub izbijen, držite ga u mlijeku ili slanoj vodi i dođite u roku od sat vremena
                    Dođite na dogovoreni nastavak liječenja
                    """,
                SuitableFor = "Jaka zubobolja, otok, povreda zuba ili usne, izgubljena plomba ili krunica.",
                DurationMinutes = 30, PriceFrom = 50, Comfort = "Prvo uklanjamo bol", ImageName = "clinic-chair",
            },
        };
        db.Services.AddRange(services);
        db.SaveChanges();
        Service S(string slug) => services.First(s => s.Slug == slug);

        var doctors = new List<Doctor>
        {
            new()
            {
                Slug = "dr-lejla-kovacevic", Name = "Dr. Lejla Kovačević", SortOrder = 1, PhotoName = "doctor-lejla", YearsExperience = 15,
                Title = "Doktor stomatologije, voditeljica ordinacije",
                Bio = "Lejla je osnovala Studio Osmijeh s idejom da posjeta zubaru bude mirna i bez iznenađenja. Najviše voli estetske plombe i izbjeljivanje, a pacijenti je pamte po strpljenju i po tome što sve objasni prije nego što počne.",
            },
            new()
            {
                Slug = "dr-tarik-begic", Name = "Dr. Tarik Begić", SortOrder = 2, PhotoName = "doctor-tarik", YearsExperience = 12,
                Title = "Specijalista oralne hirurgije i implantologije",
                Bio = "Tarik je ugradio više stotina implantata i izvadio nebrojeno umnjaka, a pacijenti kažu da su ostali iznenađeni koliko je to prošlo lako. Redovno se usavršava u inostranstvu.",
            },
            new()
            {
                Slug = "dr-mia-delic", Name = "Dr. Mia Delić", SortOrder = 3, PhotoName = "doctor-mia", YearsExperience = 8,
                Title = "Specijalista ortodoncije",
                Bio = "Mia ispravlja osmijehe kod djece, tinejdžera i odraslih. Posebno se bavi prozirnim folijama i uvijek pokaže kako će vam zubi izgledati na kraju terapije prije nego što počnete.",
            },
            new()
            {
                Slug = "dr-adnan-softic", Name = "Dr. Adnan Softić", SortOrder = 4, PhotoName = "doctor-adnan", YearsExperience = 10,
                Title = "Doktor stomatologije, opća i dječja stomatologija",
                Bio = "Adnan ima dvoje djece pa zna kako se pregovara s petogodišnjakom u stolici. Zbog njegovog mirnog pristupa kod njega najčešće dolaze porodice, od najmlađih do bake i djeda.",
            },
        };
        Doctor D(string slug) => doctors.First(d => d.Slug == slug);

        void Offers(string doctor, params string[] slugs) =>
            D(doctor).Services.AddRange(slugs.Select(s => new DoctorService { Service = S(s) }));

        Offers("dr-lejla-kovacevic", "pregled", "ciscenje-kamenca", "plombe", "lijecenje-kanala", "izbjeljivanje", "krunice-mostovi");
        Offers("dr-tarik-begic", "pregled", "implantati", "vadjenje-zuba", "krunice-mostovi", "hitni-slucaj");
        Offers("dr-mia-delic", "pregled", "ortodoncija", "djecja-stomatologija");
        Offers("dr-adnan-softic", "pregled", "ciscenje-kamenca", "plombe", "djecja-stomatologija", "vadjenje-zuba", "hitni-slucaj");

        static TimeSpan T(int h, int m = 0) => new(h, m, 0);
        void Work(string doctor, DayOfWeek[] days, TimeSpan from, TimeSpan to) =>
            D(doctor).Shifts.AddRange(days.Select(day => new DoctorShift { Day = day, Start = from, End = to }));

        DayOfWeek[] Days(params DayOfWeek[] d) => d;
        const DayOfWeek Mon = DayOfWeek.Monday, Tue = DayOfWeek.Tuesday, Wed = DayOfWeek.Wednesday,
                        Thu = DayOfWeek.Thursday, Fri = DayOfWeek.Friday, Sat = DayOfWeek.Saturday;

        Work("dr-lejla-kovacevic", Days(Mon, Tue, Thu), T(9), T(12));
        Work("dr-lejla-kovacevic", Days(Mon, Tue, Thu), T(13), T(17));
        Work("dr-lejla-kovacevic", Days(Fri), T(9), T(14));

        Work("dr-tarik-begic", Days(Tue, Wed, Fri), T(10), T(13));
        Work("dr-tarik-begic", Days(Tue, Wed, Fri), T(14), T(18));

        Work("dr-mia-delic", Days(Mon, Wed, Thu), T(13), T(19));
        Work("dr-mia-delic", Days(Sat), T(9), T(13));

        Work("dr-adnan-softic", Days(Mon, Tue, Wed, Thu, Fri), T(8), T(12));
        Work("dr-adnan-softic", Days(Mon, Tue, Wed, Thu, Fri), T(13), T(16));

        db.Doctors.AddRange(doctors);
        db.SaveChanges();
    }

    public static void AddArticles(ClinicDb db)
    {
        db.Articles.AddRange(
            new Article
            {
                Slug = "kako-pravilno-prati-zube", Title = "Kako pravilno prati zube: 7 grešaka koje većina pravi",
                Category = "Svakodnevna njega", ImageName = "brushing", ReadMinutes = 4, PublishedAt = new DateTime(2026, 9, 12),
                Excerpt = "Većina ljudi pere zube svaki dan, a ipak ima karijes. Razlog je skoro uvijek tehnika, ne količina.",
                Body = """
                    Pranje zuba izgleda kao najjednostavnija stvar na svijetu. Ipak, ordinacije svaki dan vide iste greške. Dobra vijest je da se sve ispravljaju za par dana.

                    ## 1. Prejako pritiskanje
                    Tvrdo ribanje ne čisti bolje, nego haba zubnu caklinu i povlači desni. Četkicu držite kao olovku, ne kao čekić. Dovoljan je lagan pritisak i male kružne kretnje.

                    ## 2. Prekratko pranje
                    Potrebna su dva minuta, a većina ljudi pere 30 do 45 sekundi. Pomaže mjerač vremena ili četkica s tajmerom.

                    ## 3. Pranje odmah poslije jela
                    Nakon kiselih namirnica (voće, sokovi, kola) caklina je privremeno omekšana. Pričekajte 30 minuta da se oporavi, a u međuvremenu isperite usta vodom.

                    ## 4. Zaboravljen konac
                    Četkica ne dopire između zuba, a tu nastaje najveći dio karijesa. Zubni konac ili interdentalne četkice jednom dnevno, najbolje uvečer.

                    ## 5. Stara četkica
                    Četkicu mijenjajte svaka tri mjeseca, ili čim joj se vlakna raširuju. Istrošena četkica jedva čisti.

                    ## 6. Previše paste
                    Za odrasle je dovoljna količina veličine zrna graška. Djeci do 6 godina samo trag paste, ali s fluoridima.

                    ## 7. Ispiranje vodom poslije pranja
                    Ne isperite usta odmah vodom: fluorid iz paste treba da ostane na zubima. Dovoljno je ispljunuti višak.

                    ## Najkraći plan
                    - Dva puta dnevno po dva minuta
                    - Konac ili interdentalne četkice jednom dnevno
                    - Pregled i čišćenje kamenca svakih šest mjeseci
                    """,
            },
            new Article
            {
                Slug = "koliko-cesto-ici-zubaru", Title = "Koliko često treba ići zubaru?",
                Category = "Prevencija", ImageName = "exam", ReadMinutes = 3, PublishedAt = new DateTime(2026, 8, 28),
                Excerpt = "Odgovor nije isti za sve, ali postoji pravilo koje štedi i zube i novac.",
                Body = """
                    Većina odraslih treba pregled jednom do dva puta godišnje. Taj razmak je kompromis: dovoljno kratak da se karijes uhvati dok je mali, a dovoljno dug da ne opterećuje budžet.

                    ## Zašto ne čekati bol
                    Karijes u početku ne boli. Kad zaboli, obično je već stigao do nerva, a to znači duže, skuplje i neugodnije liječenje. Pregled svakih šest mjeseci je jeftina zaštita.

                    ## Ko treba češće
                    - Osobe s paradentozom ili krvarenjem desni
                    - Pušači
                    - Trudnice (hormoni utiču na desni)
                    - Osobe s dijabetesom
                    - Svi s ortodontskim aparatićem
                    - Oni koji često imaju karijes

                    ## Djeca
                    Prvi pregled kad izbiju prvi zubi, a zatim svakih šest mjeseci. Tako dijete odrasta bez straha od zubara.

                    ## Šta se dešava na redovnom pregledu
                    Doktor provjerava zube, desni i sluznicu, a jednom godišnje se radi i snimak. Uz to se čisti kamenac, jer ga kod kuće nije moguće ukloniti.
                    """,
            },
            new Article
            {
                Slug = "izbjeljivanje-zuba-mitovi", Title = "Izbjeljivanje zuba: šta je sigurno, a šta mit",
                Category = "Estetika", ImageName = "smile-2", ReadMinutes = 4, PublishedAt = new DateTime(2026, 8, 10),
                Excerpt = "Soda bikarbona, limun, ugljen: šta stvarno radi, a šta nepovratno oštećuje caklinu.",
                Body = """
                    Svi žele bijeli osmijeh, a na internetu kruže savjeti koji više štete nego koriste. Evo šta kažu stomatolozi.

                    ## Mit 1: Limun i soda bikarbona izbjeljuju
                    Limun je kiselina koja otapa caklinu, a soda je abrazivna i grebe površinu. Zubi na kratko izgledaju svjetliji, a dugoročno postaju žućkasti i osjetljivi, jer caklina tanji i otkriva žuti dentin ispod.

                    ## Mit 2: Aktivni ugljen je siguran
                    Nema dokaza da izbjeljuje, a gruba struktura može oštetiti caklinu.

                    ## Šta radi
                    Profesionalno izbjeljivanje gelom na bazi karbamid ili vodonik peroksida pod nadzorom doktora. Gel prodire u caklinu i razlaže pigmente, a desni su zaštićene.

                    ## Ko ne treba izbjeljivati
                    - Trudnice i dojilje
                    - Djeca
                    - Osobe s neliječenim karijesom ili upalom desni (prvo se liječi)

                    ## Realna očekivanja
                    Zubi mogu postati 3 do 8 tonova svjetliji, ali plombe i krunice ne mijenjaju boju. Efekat traje 1 do 3 godine, a produžite ga manje kafe, čaja i cigareta.
                    """,
            },
            new Article
            {
                Slug = "prvi-posjet-djeteta-zubaru", Title = "Prvi posjet djeteta zubaru: kako ga pripremiti",
                Category = "Djeca", ImageName = "kids-2", ReadMinutes = 4, PublishedAt = new DateTime(2026, 7, 24),
                Excerpt = "Strah od zubara se ne rađa s djetetom, nego se uči. Evo kako da to izbjegnete.",
                Body = """
                    Stomatolozi preporučuju da dijete prvi put dođe kad mu izbije prvi zub, a najkasnije do prvog rođendana. Cilj prvog posjeta nije liječenje, nego upoznavanje.

                    ## Šta roditelj treba (ne) reći
                    Nikad ne prijetite zubarom ("ako ne budeš prao zube, ide se kod zubara") i izbjegavajte riječi kao što su "boli", "injekcija" i "ne boj se". Dijete čuje samo "boj se". Recite: "Doktor će pogledati tvoje zube i prebrojati ih."

                    ## Kako se pripremiti
                    - Zakažite termin u vrijeme kad je dijete naspavano i nije gladno
                    - Pročitajte zajedno slikovnicu o zubaru
                    - Igrajte se "zubara" kod kuće, dijete može pregledati vas
                    - Budite miran: djeca osjete tremu roditelja

                    ## Kako izgleda pregled
                    Doktor se predstavi, pokaže stolicu koja se podiže i spušta, ispriča šta radi i tek tada pregleda zube. Roditelj uvijek ostaje uz dijete.

                    ## Poslije posjeta
                    Pohvalite dijete i ne obećavajte nagradu u obliku slatkiša. Bolja je naljepnica ili igračka.
                    """,
            },
            new Article
            {
                Slug = "implantat-ili-most", Title = "Zubni implantat ili most: šta odabrati?",
                Category = "Implantati", ImageName = "implant", ReadMinutes = 5, PublishedAt = new DateTime(2026, 7, 8),
                Excerpt = "Dva načina da nadoknadite zub koji nedostaje. Razlike su veće nego što izgleda.",
                Body = """
                    Kad izgubite zub, rupa nije samo estetski problem. Susjedni zubi se naginju, suprotni zub "izrasta", a kost na tom mjestu polako nestaje. Zato je dobro ne čekati.

                    ## Most
                    Most je niz od tri spojena zuba: krunice na susjednim zubima nose zub u sredini. Gotov je brzo, za dvije do tri sedmice, ali traži brušenje dva zdrava zuba. Traje oko 10 do 15 godina.

                    ## Implantat
                    Titanijski korijen se ugrađuje u kost, a na njega ide krunica. Postupak traje 3 do 6 mjeseci zbog srastanja s kosti. Ne dira susjedne zube i čuva kost. Uz dobru njegu može trajati decenijama.

                    ## Poređenje
                    - Brzina: most je brži
                    - Početna cijena: most je jeftiniji
                    - Zdravlje susjednih zubi: implantat ne dira ništa
                    - Kost: implantat je čuva, most ne
                    - Dugovječnost: implantat obično duže traje

                    ## Šta odlučuje
                    Količina kosti, zdravlje desni, pušenje, vaša očekivanja i budžet. Zato prvo treba pregled i snimak, pa tek onda odluka. Na konzultaciji dobijate oba plana s cijenama.
                    """,
            },
            new Article
            {
                Slug = "folije-ili-aparatic", Title = "Prozirne folije ili fiksni aparatić?",
                Category = "Ortodoncija", ImageName = "aligner", ReadMinutes = 4, PublishedAt = new DateTime(2026, 6, 20),
                Excerpt = "Oba ispravljaju zube. Razlikuju se u cijeni, vidljivosti i disciplini koju traže od vas.",
                Body = """
                    Ortodoncija danas nudi dvije glavne opcije. Nijedna nije "bolja" za sve, nego zavisi od slučaja i od vas.

                    ## Fiksni aparatić
                    Bravice se lijepe na zube i spajaju žicom. Radi i kod složenih slučajeva i ne zavisi od vaše discipline jer ga ne možete skinuti. S druge strane, vidljiv je, traži oprezno čišćenje i neka hrana je zabranjena.

                    ## Prozirne folije
                    Niz providnih kalupa koje mijenjate svakih nekoliko sedmica. Skoro su nevidljive, skidaju se za jelo i pranje zuba i ugodnije su. Ali ih morate nositi 22 sata dnevno, a za neke teže slučajeve nisu dovoljne.

                    ## Pitanja za odluku
                    - Koliko vam smeta vidljivost?
                    - Možete li biti disciplinovani i nositi folije cijeli dan?
                    - Koliko je težak vaš slučaj? To procjenjuje ortodont
                    - Koliki je budžet?

                    ## Zašto retainer
                    Poslije svake terapije zubi teže da se vrate, pa se nosi retainer (tanka žica ili folija). Bez njega se rezultat polako gubi.
                    """,
            },
            new Article
            {
                Slug = "zubobolja-usred-noci", Title = "Šta uraditi kad zub zaboli usred noći",
                Category = "Hitni slučajevi", ImageName = "toothbrush-red", ReadMinutes = 3, PublishedAt = new DateTime(2026, 5, 30),
                Excerpt = "Kratak vodič za prvu pomoć dok ne dođete do zubara.",
                Body = """
                    Zubobolja najčešće počne u najgore vrijeme: noću, vikendom, na putu. Ovo možete uraditi do dolaska kod doktora.

                    ## Odmah
                    - Isperite usta mlakom slanom vodom
                    - Zubnim koncem pažljivo uklonite zaglavljenu hranu
                    - Hladan oblog sa vanjske strane obraza, 15 minuta
                    - Uzmite uobičajeni analgetik (ibuprofen ili paracetamol) prema uputi

                    ## Šta nikako
                    - Ne stavljajte aspirin direktno na zub ili desni: izaziva opekotinu
                    - Ne koristite toplotu na oteklini
                    - Ne čekajte da "prođe sama"

                    ## Kada je hitno
                    Odmah se javite ako imate otok lica ili vrata, temperaturu, otežano gutanje ili otvaranje usta. To mogu biti znaci raširene infekcije.

                    ## Izbijen zub
                    Držite zub za krunicu, a ne za korijen. Isperite ga mlijekom, ne vodom i stavite ga u mlijeko ili ispod jezika. Dođite u roku od sat vremena, jer je tad velika šansa da se zub vrati.
                    """,
            });
        db.SaveChanges();
    }
}
