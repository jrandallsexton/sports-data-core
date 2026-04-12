// Connect: mongosh "mongodb://admin:SportDeets2025!!!@192.168.0.250:27017/admin" --authenticationDatabase admin

use FootballNfl

db.getCollectionNames().forEach(c => { print("Dropping: " + c); db[c].drop(); })

db.getCollectionNames()
