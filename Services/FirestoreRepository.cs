using BigLocalHub.Models;
using Plugin.Firebase.Firestore;

namespace BigLocalHub.Services;

/// <summary>
/// Port of the useCollection hook in packages/core/hooks.ts.
///
/// The React version returns [docs, {add, update, remove}] and keeps docs live
/// via onSnapshot. Here, Watch() returns an IDisposable subscription and pushes
/// each new snapshot to a callback — the view model turns that into an
/// ObservableCollection.
///
/// The default ordering is by createdAt ascending, matching the hook, so lists
/// come back in the same order as the web and Expo apps.
/// </summary>
public class FirestoreRepository
{
    private readonly IFirebaseFirestore _db;

    public FirestoreRepository(IFirebaseFirestore db) => _db = db;

    /// <summary>
    /// Live subscription to a collection. Dispose the return value to stop
    /// listening — leaving it running keeps a Firestore stream (and the view
    /// model it captures) alive for the life of the app.
    /// </summary>
    public IDisposable Watch<T>(
        string collectionPath,
        Action<IReadOnlyList<T>> onChanged,
        Action<Exception>? onError = null,
        string orderByField = "createdAt")
        where T : FirestoreDocument
    {
        return _db.GetCollection(collectionPath)
                  .OrderBy(orderByField, false)
                  .AddSnapshotListener<T>(
                      snapshot =>
                      {
                          var items = snapshot.Documents
                              .Select(d => d.Data)
                              .Where(d => d is not null)
                              .Select(d => d!)
                              .ToList();
                          onChanged(items);
                      },
                      ex =>
                      {
                          // A listener error is almost always a rules failure.
                          // Surfacing it matters: silently swallowing leaves an
                          // empty list that reads as "no data" instead of
                          // "not allowed".
                          System.Diagnostics.Debug.WriteLine($"[firestore] {collectionPath} listener failed: {ex}");
                          onError?.Invoke(ex);
                      });
    }

    /// <summary>
    /// One-shot read of a single document, for screens backed by one doc per
    /// tenant (e.g. seoHealth/{companyId}) rather than a collection. Returns
    /// null if the document doesn't exist yet — e.g. a company that hasn't
    /// had its first scheduled scan.
    /// </summary>
    public async Task<T?> GetDocAsync<T>(string documentPath) where T : FirestoreDocument
    {
        var snap = await _db.GetDocument(documentPath).GetDocumentSnapshotAsync<T>();
        return snap.Data;
    }

    /// <summary>One-shot read, for cases that don't need to stay live.</summary>
    public async Task<IReadOnlyList<T>> GetAsync<T>(string collectionPath, string orderByField = "createdAt")
        where T : FirestoreDocument
    {
        var snapshot = await _db.GetCollection(collectionPath)
                                .OrderBy(orderByField, false)
                                .GetDocumentsAsync<T>();
        return snapshot.Documents
            .Select(d => d.Data)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList();
    }

    /// <summary>
    /// One-shot equality query. Deliberately not ordered: adding an orderBy to
    /// a where-clause needs a composite index, and this is used for small
    /// result sets (a single company's users) where sorting client-side is
    /// cheaper than maintaining one.
    /// </summary>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string collectionPath, string field, object value)
        where T : FirestoreDocument
    {
        var snapshot = await _db.GetCollection(collectionPath)
                                .WhereEqualsTo(field, value)
                                .GetDocumentsAsync<T>();
        return snapshot.Documents
            .Select(d => d.Data)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList();
    }

    /// <summary>
    /// Adds a document with a server-side createdAt, matching the hook's add().
    /// The timestamp has to be server-side: ordering depends on it, and a
    /// device clock that is even slightly off would sort the list wrong.
    /// </summary>
    public async Task<string> AddAsync<T>(string collectionPath, T data) where T : FirestoreDocument
    {
        var doc = await _db.GetCollection(collectionPath).AddDocumentAsync(data);
        await doc.UpdateDataAsync(("createdAt", FieldValue.ServerTimestamp()));
        return doc.Id;
    }

    /// <summary>
    /// Field-level update plus updatedAt, matching the hook's update().
    /// Deliberately NOT a whole-document set: these models map only the fields
    /// this app uses, so overwriting the document would drop everything else
    /// on it (cadence state, portal links, import batch id).
    /// </summary>
    public Task UpdateAsync(string collectionPath, string id, params (string Field, object Value)[] fields)
    {
        var payload = fields
            .Append(("updatedAt", (object)FieldValue.ServerTimestamp()))
            .ToArray();
        return _db.GetDocument($"{collectionPath}/{id}").UpdateDataAsync(payload);
    }

    public Task RemoveAsync(string collectionPath, string id) =>
        _db.GetDocument($"{collectionPath}/{id}").DeleteDocumentAsync();
}
