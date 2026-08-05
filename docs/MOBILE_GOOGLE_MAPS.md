# Carte Google Maps du client mobile

La carte de l'adresse d'intervention et la carte de suivi du prestataire utilisent le composant officiel .NET MAUI Maps et le SDK Google Maps Android.

Le suivi client se rafraîchit automatiquement. L'application prestataire partage sa position uniquement au premier plan, après confirmation du paiement et jusqu'au démarrage de l'intervention. La même clé Android couvre les deux cartes du client mobile.

## Configuration locale

La clé n'est jamais stockée dans le dépôt. Activer **Maps SDK for Android** dans Google Cloud, créer une clé limitée à l'application Android `ci.wele.client`, puis compiler avec l'une de ces deux méthodes :

```powershell
$env:GOOGLE_MAPS_API_KEY = "votre-cle-android-restreinte"
dotnet build src/HomeService.Client.Mobile/HomeService.Client.Mobile.csproj -f net9.0-android
```

ou :

```powershell
dotnet build src/HomeService.Client.Mobile/HomeService.Client.Mobile.csproj -f net9.0-android -p:GoogleMapsApiKey="votre-cle-android-restreinte"
```

Sans clé, le projet continue de compiler afin que la validation automatique Windows reste opérationnelle, mais Google ne fournit pas les tuiles de carte dans l'application Android.

## Production

La clé de production doit être fournie par le coffre de secrets de la chaîne de livraison. Elle doit être limitée :

- à **Maps SDK for Android** uniquement ;
- au nom de package `ci.wele.client` ;
- aux empreintes SHA-1 des certificats de signature utilisés pour les APK de test et de production.

La clé serveur utilisée pour Google Places ne doit pas être réutilisée dans l'application mobile.
