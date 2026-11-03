using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Core;
using System.Text.Json;
using Cognito = Amazon.CognitoIdentityProvider.Model;
using Dynamo = Amazon.DynamoDBv2.Model;

namespace Restaurant_Aqa_Framework.Api.Seeding;

public sealed class OwnedGroupCleanup : IAsyncDisposable
{
    private readonly AmazonCognitoIdentityProviderClient _cognito;
    private readonly AmazonDynamoDBClient _database;
    private readonly string _poolId;
    private readonly string _accountId;
    private readonly string _region;
    private readonly string _manifest;
    private bool _disposed;
    private readonly HashSet<string> _reservationIds = [];
    private readonly HashSet<string> _groupIds = [];
    private readonly HashSet<string> _waiterIds = [];

    public string RunId { get; } = Guid.NewGuid().ToString("N");
    public string Username => $"aqa.sample.{RunId}@example.com";
    public string? OwnerId { get; private set; }
    public string? TableId { get; private set; }
    public string? LocationId { get; private set; }

    public OwnedGroupCleanup()
    {
        EnvConfig.EnsureLoaded();
        _region = Required("AWS_REGION");
        _accountId = Required("AQA_AWS_ACCOUNT_ID");
        _poolId = Required("AQA_COGNITO_USER_POOL_ID");
        var endpoint = RegionEndpoint.GetBySystemName(_region);
        _cognito = new AmazonCognitoIdentityProviderClient(endpoint);
        _database = new AmazonDynamoDBClient(endpoint);
        var directory = Directory.CreateDirectory(Path.Combine(ArtifactPaths.Root, "owned-data"));
        _manifest = Path.Combine(directory.FullName, RunId + ".json");
        SaveManifest(false);
    }

    public async Task CreateCustomerAsync(string password)
    {
        var pool = await _cognito.DescribeUserPoolAsync(new Cognito.DescribeUserPoolRequest { UserPoolId = _poolId });
        if (pool.UserPool.Arn != $"arn:aws:cognito-idp:{_region}:{_accountId}:userpool/{_poolId}")
            throw new InvalidOperationException("The AWS account, region and user pool do not match the fixture configuration.");
        var created = await _cognito.AdminCreateUserAsync(new Cognito.AdminCreateUserRequest
        {
            UserPoolId = _poolId,
            Username = Username,
            MessageAction = MessageActionType.SUPPRESS,
            UserAttributes =
            [
                new() { Name = "email", Value = Username },
                new() { Name = "given_name", Value = SampleDataFactory.Prefix + "Customer" },
                new() { Name = "family_name", Value = "Customer" },
            ],
        });
        OwnerId = created.User.Attributes.Single(attribute => attribute.Name == "sub").Value;
        SaveManifest(false);
        await _cognito.AdminSetUserPasswordAsync(new Cognito.AdminSetUserPasswordRequest
        {
            UserPoolId = _poolId,
            Username = Username,
            Password = password,
            Permanent = true,
        });
        await _cognito.AdminAddUserToGroupAsync(new Cognito.AdminAddUserToGroupRequest
        {
            UserPoolId = _poolId,
            Username = Username,
            GroupName = "Customer",
        });
    }

    public async Task<string> GetWaiterLocationAsync(string waiterId, string configuredTableId)
    {
        var waiter = await GetAsync("Waiters", "userId", waiterId);
        var table = await GetAsync("Tables", "id", configuredTableId);
        if (waiter.Count == 0 || table.Count == 0 || Value(waiter, "locationId") != Value(table, "locationId"))
            throw new InvalidOperationException("The configured waiter and table must belong to the same location.");
        return Value(table, "locationId");
    }

    public void TrackTable(string tableId, string locationId)
    {
        TableId = tableId;
        LocationId = locationId;
        SaveManifest(false);
    }

    public async Task PrepareEmptySubmittedCartAsync(string groupId, string sessionId)
    {
        await ValidateCustomerAsync();
        var group = await GetAsync("GroupReservations", "groupReservationId", groupId);
        var reservation = await GetAsync("Reservations", "id", Value(group, "reservationId"));
        var session = await GetAsync("GuestSessions", "guestSessionId", sessionId);
        if (Value(reservation, "customerId") != OwnerId || Value(reservation, "tableId") != TableId ||
            Value(session, "groupReservationId") != groupId)
            throw new InvalidOperationException("Refusing to prepare a cart outside this fixture.");
        // Public cart editing prevents this defensive-validation state; change only the owned session.
        await _database.UpdateItemAsync(new Dynamo.UpdateItemRequest
        {
            TableName = "GuestSessions",
            Key = Key("guestSessionId", sessionId),
            UpdateExpression = "SET #dishes = :empty",
            ConditionExpression = "groupReservationId = :group AND #submitted = :submitted AND #dishes = :previous",
            ExpressionAttributeNames = new() { ["#dishes"] = "dishes", ["#submitted"] = "isSubmitted" },
            ExpressionAttributeValues = new()
            {
                [":empty"] = new() { L = [] },
                [":group"] = new() { S = groupId },
                [":submitted"] = session["isSubmitted"],
                [":previous"] = session["dishes"],
            },
        });
    }

    public async Task CleanUpAsync()
    {
        if (OwnerId is not null)
        {
            await ValidateCustomerAsync();
            var reservations = await FindAsync("Reservations", "customerId", OwnerId);
            foreach (var reservation in reservations)
            {
                var reservationId = Value(reservation, "id");
                _reservationIds.Add(reservationId);
                _waiterIds.Add(Value(reservation, "waiterId"));
                SaveManifest(false);
                foreach (var group in await FindAsync("GroupReservations", "reservationId", reservationId))
                {
                    var groupId = Value(group, "groupReservationId");
                    _groupIds.Add(groupId);
                    SaveManifest(false);
                    await RemoveMatchesAsync("GuestSessions", "groupReservationId", groupId, "guestSessionId");
                    await DeleteAsync("GroupReservations", Key("groupReservationId", groupId), "reservationId", reservationId);
                }
                foreach (var order in await FindAsync("Orders", "reservationId", reservationId))
                {
                    if (Value(order, "customerId") != OwnerId)
                        throw new InvalidOperationException("Refusing to clean an order owned by another customer.");
                    var orderKey = Key("orderId", Value(order, "orderId"));
                    orderKey.Add("customerId", new Dynamo.AttributeValue { S = OwnerId });
                    await DeleteAsync("Orders", orderKey, "reservationId", reservationId);
                }
                await RemoveMatchesAsync("Notifications", "reservationId", reservationId, "id");
                await RemoveWaiterReferenceAsync(Value(reservation, "waiterId"), reservationId);
                await DeleteAsync("Reservations", Key("id", reservationId), "customerId", OwnerId);
            }
        }
        if (TableId is not null)
            await DeleteAsync("Tables", Key("id", TableId), "locationId", LocationId!);
        if (OwnerId is not null)
        {
            await ValidateCustomerAsync();
            await _cognito.AdminDeleteUserAsync(new Cognito.AdminDeleteUserRequest { UserPoolId = _poolId, Username = Username });
            OwnerId = null;
        }
        SaveManifest(true);
    }

    public async Task<bool> IsRemovedAsync()
    {
        var manifest = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(_manifest));
        var originalOwner = manifest.GetProperty("ownerId").GetString();
        if (originalOwner is not null && (await FindAsync("Reservations", "customerId", originalOwner)).Count != 0)
            return false;
        if (TableId is not null && (await GetAsync("Tables", "id", TableId)).Count != 0)
            return false;
        foreach (var groupId in _groupIds)
            if ((await GetAsync("GroupReservations", "groupReservationId", groupId)).Count != 0 ||
                (await FindAsync("GuestSessions", "groupReservationId", groupId)).Count != 0)
                return false;
        foreach (var reservationId in _reservationIds)
            if ((await FindAsync("Orders", "reservationId", reservationId)).Count != 0 ||
                (await FindAsync("Notifications", "reservationId", reservationId)).Count != 0)
                return false;
        foreach (var waiterId in _waiterIds)
        {
            var waiter = await GetAsync("Waiters", "userId", waiterId);
            if (waiter.Count == 0) return false;
            if (waiter.TryGetValue("reservations", out var references) &&
                ReferenceIds(references).Any(_reservationIds.Contains))
                return false;
        }
        try
        {
            await _cognito.AdminGetUserAsync(new Cognito.AdminGetUserRequest { UserPoolId = _poolId, Username = Username });
            return false;
        }
        catch (Cognito.UserNotFoundException)
        {
            return true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try { await CleanUpAsync(); }
        finally
        {
            _cognito.Dispose();
            _database.Dispose();
            _disposed = true;
        }
    }

    private async Task ValidateCustomerAsync()
    {
        var user = await _cognito.AdminGetUserAsync(new Cognito.AdminGetUserRequest { UserPoolId = _poolId, Username = Username });
        if (user.UserAttributes.Single(attribute => attribute.Name == "sub").Value != OwnerId ||
            user.UserAttributes.Single(attribute => attribute.Name == "given_name").Value != SampleDataFactory.Prefix + "Customer")
            throw new InvalidOperationException("Refusing cleanup: test customer ownership could not be verified.");
    }

    private async Task RemoveWaiterReferenceAsync(string waiterId, string reservationId)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var waiter = await GetAsync("Waiters", "userId", waiterId);
            if (!waiter.TryGetValue("reservations", out var previous) || !ReferenceIds(previous).Contains(reservationId))
                return;
            var remaining = ReferenceIds(previous).Where(value => value != reservationId).ToList();
            var values = new Dictionary<string, Dynamo.AttributeValue> { [":previous"] = previous };
            if (remaining.Count > 0)
                values[":remaining"] = previous.SS is not null
                    ? new Dynamo.AttributeValue { SS = remaining }
                    : new Dynamo.AttributeValue { L = remaining.Select(value => new Dynamo.AttributeValue { S = value }).ToList() };
            try
            {
                await _database.UpdateItemAsync(new Dynamo.UpdateItemRequest
                {
                    TableName = "Waiters",
                    Key = Key("userId", waiterId),
                    UpdateExpression = remaining.Count == 0 ? "REMOVE #items" : "SET #items = :remaining",
                    ConditionExpression = "#items = :previous",
                    ExpressionAttributeNames = new() { ["#items"] = "reservations" },
                    ExpressionAttributeValues = values,
                });
                return;
            }
            catch (Dynamo.ConditionalCheckFailedException) when (attempt < 4) { }
        }
    }

    private async Task RemoveMatchesAsync(string table, string attribute, string value, string primaryKey)
    {
        foreach (var item in await FindAsync(table, attribute, value))
            await DeleteAsync(table, Key(primaryKey, Value(item, primaryKey)), attribute, value);
    }

    private async Task<List<Dictionary<string, Dynamo.AttributeValue>>> FindAsync(string table, string attribute, string value)
    {
        var request = new Dynamo.ScanRequest
        {
            TableName = table,
            ConsistentRead = true,
            FilterExpression = "#field = :value",
            ExpressionAttributeNames = new() { ["#field"] = attribute },
            ExpressionAttributeValues = new() { [":value"] = new Dynamo.AttributeValue { S = value } },
        };
        var items = new List<Dictionary<string, Dynamo.AttributeValue>>();
        do
        {
            var response = await _database.ScanAsync(request);
            if (response.Items is not null) items.AddRange(response.Items);
            request.ExclusiveStartKey = response.LastEvaluatedKey;
        } while (request.ExclusiveStartKey is { Count: > 0 });
        return items;
    }

    private async Task<Dictionary<string, Dynamo.AttributeValue>> GetAsync(string table, string key, string value) =>
        (await _database.GetItemAsync(new Dynamo.GetItemRequest { TableName = table, Key = Key(key, value), ConsistentRead = true })).Item ?? [];

    private Task<Dynamo.DeleteItemResponse> DeleteAsync(string table, Dictionary<string, Dynamo.AttributeValue> key, string attribute, string expected) =>
        _database.DeleteItemAsync(new Dynamo.DeleteItemRequest
        {
            TableName = table,
            Key = key,
            ConditionExpression = "attribute_not_exists(#key) OR #owner = :expected",
            ExpressionAttributeNames = new() { ["#key"] = key.Keys.First(), ["#owner"] = attribute },
            ExpressionAttributeValues = new() { [":expected"] = new Dynamo.AttributeValue { S = expected } },
        });

    private void SaveManifest(bool cleaned)
    {
        var previousOwner = File.Exists(_manifest)
            ? JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(_manifest)).GetProperty("ownerId").GetString() : null;
        File.WriteAllText(_manifest, JsonSerializer.Serialize(new
        {
            runId = RunId,
            username = Username,
            ownerId = OwnerId ?? previousOwner,
            tableId = TableId,
            locationId = LocationId,
            cleaned,
            reservationIds = _reservationIds,
            groupIds = _groupIds,
            waiterIds = _waiterIds,
        }));
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw new InvalidOperationException($"Set {name} in the local .env or CI environment for owned test-data cleanup.");

    private static Dictionary<string, Dynamo.AttributeValue> Key(string name, string value) => new() { [name] = new() { S = value } };
    private static string Value(Dictionary<string, Dynamo.AttributeValue> item, string name) => item[name].S;
    private static List<string> ReferenceIds(Dynamo.AttributeValue value) =>
        value.SS ?? value.L?.Select(item => item.S).ToList() ?? [];
}
