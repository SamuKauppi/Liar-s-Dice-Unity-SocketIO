const io = require('socket.io')(8000, {
    pingInterval: 30000,        // Kuinka usein pingataan
    pingTimeout: 5000,          // Kuinka kauan odotetaan
    upgradeTimeout: 3000,       // liittyy johonkin päivittämiseen
    allowUpgrades: true,        // tulisi olla true jos webpohjainen client
    cookie: false,              // keksit
    serverClient: true,         // Ei meille väliä, liittyy webpohjaisiin servererihin
    cors: {
        origin: "*"  // * antaa luvan kaikille yhteyden
    }

});

const mysql = require('mysql2');

// Create the connection to the database (you can reuse this for multiple queries)
const connection = mysql.createConnection({
    host: 'localhost',    // Your database host
    user: 'root',         // Your database user
    password: '',         // Your database password
    database: 'liarsdice' // Your database name
});

// Function to insert values into the player table
function insertPlayer(name, bid_count, bid_dice, was_liar, was_caller) {
    const sql = `INSERT INTO player (name, bid_count, bid_dice, was_liar, was_caller) VALUES (?, ?, ?, ?, ?)`;

    // Execute the query
    connection.query(sql, [name, bid_count, bid_dice, was_liar, was_caller], (err, results) => {
        if (err) {
            console.error('Error inserting data:', err);
            return;
        }
        console.log('Data inserted, ID:', results.insertId);
    });
}

function wasBidCorrect() {
    let sum = 0;

    // Loop through each player's dice
    players.forEach((player) => {
        // Loop through each dice of the player
        player.dices.forEach((dice) => {
            // Check if the dice matches the bid face (l_dice) or is a wild (1)
            if (dice === l_dice || dice === 1) {
                sum++;
            }
        });
    });

    // Return true if the total number of matching dice meets or exceeds the bid quantity
    return sum >= l_count;
}

function generateName() {
    const syllables = ['la', 'ne', 'vo', 'ka', 'ra', 'mi', 'to', 'sa', 'di', 'lu'];
    let name = '';

    for (let i = 0; i < 3; i++) {  // Adjust 3 to change the length of the name
        let randomIndex = Math.floor(Math.random() * syllables.length);
        name += syllables[randomIndex];
    }

    return name.charAt(0).toUpperCase() + name.slice(1);  // Capitalize the first letter
}

function rollDices() {
    let diceRolls = [];
    for (let i = 0; i < 5; i++) {
        let roll = Math.floor(Math.random() * 6) + 1;
        diceRolls.push(roll);
    }
    console.log("Dice rolls: " + diceRolls);
    return diceRolls;
}


console.log("Käynnistetään socket.io serveri");
var players = [];       // Pitää kirjaa kaikista pelaajista. Sisältää player objekteja
var player = [];        // Javascript objekti yksittäisestä pelaajasta   
var currentplayer = 0;  // Vuoro pelaaja (id numero)
var nextplayer;         // Seuraava pelaaja, jolle lähetetään tietoa tarpeen mukaan

// During bidding we need these variables
var l_count = 0;
var l_dice = 0;
var totalDiceCount = 0;

io.on('connection', (socket) => {
    // Unitystä otetaan pysyvä yhteys js serveri generoi client id
    console.log(new Date().toUTCString() + " Unity yhdistää js socket ID on " + socket.id);


    // Kun pelaaja liittyy, luodaan sille hahmo
    socket.on('CREATEPLAYER', (data) => {

        // Check if the number of players is already 4
        if (players.length >= 4) {
            // Inform the client that the maximum number of players has been reached
            socket.emit('ERROR', 'Maximum number of players (4) has been reached.');
            return;  // Stop further execution
        }

        // Tämä ajetaan, kun clientti pyytää pelaajan luontia
        // Ennenkuin tehdään uusi pelaaja, katsotaan mitä muita pelaajoita serverillä on
        // Ja luodaaan ne tälle uudelle clientille

        players.forEach((item) => {

            socket.emit('INSTANCEPLAYER', JSON.stringify(item));
        });

        player = {
            socketID: socket.id,
            playerPos: players.length,
            playerName: generateName(),
            dices: rollDices(),
            bid: [
                0,
                0
            ]
        };

        // Laitetaan luotu pelaaja players taulukkoon
        players.push(player);

        // Kustutaan Unityssä, että luo pelaaja ja lähetetään dataa sijainnista
        // Tieto uudesta pelaajasta ja sen luonnista lähetetään kaikille clienteille
        io.emit('INSTANCEPLAYER', JSON.stringify(player));

        // Kun pelaaja lisätään taulukkoon, tarkastetaan, onko pelaaja ainut talukossa
        // Jos on annetaaan kyseisellä annetaan vuoro
        if (players.length == 1) {
            // Kerrotaan että on pelaajan vuoro
            io.to(players[0].socketID).emit("STARTTURN", JSON.stringify(player));
        }

        totalDiceCount += 5;
    });

    socket.on('MAKEBID', (data) => {

        console.log("Pelaaja tekee bid " + data["bid"][0] + " x " + data["bid"][1]);

        // Tee serverille logiikko voiko vuoron päättää
        // jos ei, kerro siitä clientille

        var count = data["bid"][0];
        var dice = data["bid"][1];

        let reason = "";  // Variable to hold the reason for invalid bid

        if (dice < 1 || dice > 6) {
            reason = "Dice value must be between 1 and 6.";
        }
        else if (count < 1 || count > totalDiceCount) {
            reason = `Count must be between 1 and ${totalDiceCount}.`;
        }
        else if (count < l_count || (count == l_count && dice <= l_dice)) {
            reason = "Bid must be higher than previous";
        }


        // Check if there's an invalid reason
        if (reason !== "") {
            // Send the reason why the bid is invalid to the client
            io.to(players[currentplayer].socketID).emit('INVALID_BID', reason);
            console.log("Bid was not valid: " + reason);
            return;  // Stop further execution if bid is invalid
        }

        console.log("Bid was valid");

        l_count = count;
        l_dice = dice;

        // Serveri on sitä mieltä, että vuoron voi päättää
        io.emit('UPDATEOTHERS', JSON.stringify(data));
        io.to(players[currentplayer].socketID).emit('MADEBID', JSON.stringify(data));
    });

    socket.on('TURNENDED', (data) => {

        console.log("Pelaajan " + currentplayer + " vuoro on päättynyt. Seuraavan vuoro");
        nextplayer = currentplayer >= players.length - 1 ? 0 : currentplayer + 1;
        console.log("Seuraavan pelaajan vuoro alkaa " + nextplayer);

        io.to(players[nextplayer].socketID).emit('STARTTURN', JSON.stringify(data));
        io.emit('OTHERSTURN', JSON.stringify(players[nextplayer]));

        // Nyt kun clientille on kerrottu, että vuoro on vaihtunut, päivitetään currentplayer
        currentplayer = nextplayer;

    });

    socket.on('CALLLIAR', _ => {

        let reason = "";  // Variable to hold the reason for invalid call

        if (l_count == 0) {
            reason = "The first player can't call liar"
        }

        if (reason != "") {
            io.to(players[currentplayer].socketID).emit('INVALID_CALL', reason);
            console.log(reason);
            return;  // Stop further execution if bid is invalid
        }

        let playerdata = {};

        players.forEach(plr => {
            playerdata[plr.socketID] = plr.dices
        });

        playerdata["bid"] = {
            count: l_count,
            dice: l_dice
        }

        let prevplayer = currentplayer <= 0 ? players.length - 1 : currentplayer - 1;
        playerdata["caller"] = players[currentplayer].playerName;
        playerdata["called"] = players[prevplayer].playerName;

        let wasCorrect = wasBidCorrect();
        let winner = !wasCorrect ? currentplayer : prevplayer;
        let loser = wasCorrect ? currentplayer : prevplayer;

        playerdata["winner"] = {
            socketID: players[winner].socketID,
            playername: players[winner].playerName
        };

        playerdata["loser"] = {
            socketID: players[loser].socketID,
            playername: players[loser].playerName
        };

        io.emit('CALLEDLIAR', JSON.stringify(playerdata));


        insertPlayer(players[winner].playerName, l_count, l_dice, wasCorrect == true ? 1 : 0, winner == currentplayer ? 1 : 0);
    });

    socket.on('disconnect', (reason) => {
        console.log(new Date().toUTCString() + " Player " + socket.id + " left the game " + reason);

        // Pelaaja lähti serveiltä, etsitään se ja poistetaan se
        for (var i = 0; i < players.length; i++) {
            if (players[i].socketID == socket.id) {
                // Splice poistaa taulukosta
                players.splice(i, 1);
                totalDiceCount -= 5;
                l_count = 0;
                l_dice = 0;
            }

            io.emit('DELETEPLAYER', socket.id)
        }

        currentplayer = currentplayer >= players.length - 1 ? 0 : currentplayer;

        if (players.length > 0) {
            // Kerrotaan että on pelaajan vuoro
            console.log("Moving the turn to: " + currentplayer);
            io.to(players[currentplayer].socketID).emit("STARTTURN", JSON.stringify(players[currentplayer]));
            io.emit('OTHERSTURN', JSON.stringify(players[currentplayer]));
        }
    });
});