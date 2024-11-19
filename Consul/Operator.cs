// -----------------------------------------------------------------------
//  <copyright file="Operator.cs" company="PlayFab Inc">
//    Copyright 2015 PlayFab Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Consul
{
    /// <summary>
    /// RaftServer has information about a server in the Raft configuration.
    /// </summary>
    public class RaftServer
    {
        /// <summary>
        /// ID is the unique ID for the server. These are currently the same
        /// as the address, but they will be changed to a real GUID in a future
        /// release of Consul.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Node is the node name of the server, as known by Consul, or this
        /// will be set to "(unknown)" otherwise.
        /// </summary>
        public string Node { get; set; }

        /// <summary>
        /// Address is the IP:port of the server, used for Raft communications.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Leader is true if this server is the current cluster leader.
        /// </summary>
        public bool Leader { get; set; }

        /// <summary>
        /// Voter is true if this server has a vote in the cluster. This might
        /// be false if the server is staging and still coming online, or if
        /// it's a non-voting server, which will be added in a future release of
        /// Consul
        /// </summary>
        public bool Voter { get; set; }
    }

    /// <summary>
    /// RaftConfigration is returned when querying for the current Raft configuration.
    /// </summary>
    public class RaftConfiguration
    {
        /// <summary>
        /// Servers has the list of servers in the Raft configuration.
        /// </summary>
        public List<RaftServer> Servers { get; set; }

        /// <summary>
        /// Index has the Raft index of this configuration.
        /// </summary>
        public ulong Index { get; set; }
    }

    /// <summary>
    /// KeyringResponse is returned when listing the gossip encryption keys
    /// </summary>
    public class KeyringResponse
    {
        /// <summary>
        /// Whether this response is for a WAN ring
        /// </summary>
        public bool WAN { get; set; }
        /// <summary>
        /// The datacenter name this request corresponds to
        /// </summary>
        public string Datacenter { get; set; }
        /// <summary>
        /// A map of the encryption keys to the number of nodes they're installed on
        /// </summary>
        public IDictionary<string, int> Keys { get; set; }
        /// <summary>
        /// The total number of nodes in this ring
        /// </summary>
        public int NumNodes { get; set; }
    }

    public class AreaRequest
    {
        /// <summary>
        /// PeerDatacenter is the peer Consul datacenter that will make up the
        /// other side of this network area. Network areas always involve a pair
        /// of datacenters: the datacenter where the area was created, and the
        /// peer datacenter. This is required.
        /// </summary>
        public string PeerDatacenter { get; set; }

        /// <summary>
        /// RetryJoin specifies the address of Consul servers to join to, such as
	    /// an IPs or hostnames with an optional port number. This is optional.
        /// </summary>
        public string[] RetryJoin { get; set; }

        /// <summary>
        /// UseTLS specifies whether gossip over this area should be encrypted with TLS
        /// if possible.
        /// </summary>
        public bool UseTLS { get; set; }
    }
    public class AreaJoinResponse
    {
        /// <summary>
        /// The address that was joined.
        /// </summary>
        public string Address { get; set; }
        /// <summary>
        /// Whether or not the join was a success.
        /// </summary>
        public bool Joined { get; set; }
        /// <summary>
        /// If we couldn't join, this is the message with information.
        /// </summary>
        public string Error { get; set; }
    }

    public class Area : AreaRequest
    {
        /// <summary>
        /// ID is this identifier for an area (a UUID).
        /// </summary>
        public string ID { get; set; }
    }
    public class Operator : IOperatorEndpoint
    {
        private readonly ConsulClient _client;

        /// <summary>
        /// Operator can be used to perform low-level operator tasks for Consul.
        /// </summary>
        /// <param name="c"></param>
        internal Operator(ConsulClient c)
        {
            _client = c;
        }

        /// <summary>
        /// KeyringRequest is used for performing Keyring operations
        /// </summary>
        private class KeyringRequest
        {
            [JsonProperty]
            internal string Key { get; set; }
        }

        /// <summary>
        /// RaftGetConfiguration is used to query the current Raft peer set.
        /// </summary>
        public Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(CancellationToken ct = default)
        {
            return RaftGetConfiguration(QueryOptions.Default, ct);
        }

        /// <summary>
        /// RaftGetConfiguration is used to query the current Raft peer set.
        /// </summary>
        public Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(QueryOptions q, CancellationToken ct = default)
        {
            return _client.Get<RaftConfiguration>("/v1/operator/raft/configuration", q).Execute(ct);
        }

        /// <summary>
        /// RaftRemovePeerByAddress is used to kick a stale peer (one that it in the Raft
        /// quorum but no longer known to Serf or the catalog) by address in the form of
        /// "IP:port".
        /// </summary>
        public Task<WriteResult> RaftRemovePeerByAddress(string address, CancellationToken ct = default)
        {
            return RaftRemovePeerByAddress(address, WriteOptions.Default, ct);
        }

        /// <summary>
        /// RaftRemovePeerByAddress is used to kick a stale peer (one that it in the Raft
        /// quorum but no longer known to Serf or the catalog) by address in the form of
        /// "IP:port".
        /// </summary>
        public Task<WriteResult> RaftRemovePeerByAddress(string address, WriteOptions q, CancellationToken ct = default)
        {
            var req = _client.Delete("/v1/operator/raft/peer", q);

            // From Consul repo:
            // TODO (slackpad) Currently we made address a query parameter. Once
            // IDs are in place this will be DELETE /v1/operator/raft/peer/<id>.
            req.Params["address"] = address;

            return req.Execute(ct);
        }

        /// <summary>
        /// KeyringInstall is used to install a new gossip encryption key into the cluster
        /// </summary>
        public Task<WriteResult> KeyringInstall(string key, CancellationToken ct = default)
        {
            return KeyringInstall(key, WriteOptions.Default, ct);
        }

        /// <summary>
        /// KeyringInstall is used to install a new gossip encryption key into the cluster
        /// </summary>
        public Task<WriteResult> KeyringInstall(string key, WriteOptions q, CancellationToken ct = default)
        {
            return _client.Post("/v1/operator/keyring", new KeyringRequest() { Key = key }, q).Execute(ct);
        }

        /// <summary>
        /// KeyringList is used to list the gossip keys installed in the cluster
        /// </summary>
        public Task<QueryResult<KeyringResponse[]>> KeyringList(CancellationToken ct = default)
        {
            return KeyringList(QueryOptions.Default, ct);
        }

        /// <summary>
        /// KeyringList is used to list the gossip keys installed in the cluster
        /// </summary>
        public Task<QueryResult<KeyringResponse[]>> KeyringList(QueryOptions q, CancellationToken ct = default)
        {
            return _client.Get<KeyringResponse[]>("/v1/operator/keyring", q).Execute(ct);
        }

        /// <summary>
        /// KeyringRemove is used to remove a gossip encryption key from the cluster
        /// </summary>
        public Task<WriteResult> KeyringRemove(string key, CancellationToken ct = default)
        {
            return KeyringRemove(key, WriteOptions.Default, ct);
        }

        /// <summary>
        /// KeyringRemove is used to remove a gossip encryption key from the cluster
        /// </summary>
        public Task<WriteResult> KeyringRemove(string key, WriteOptions q, CancellationToken ct = default)
        {
            return _client.DeleteAccepting("/v1/operator/keyring", new KeyringRequest() { Key = key }, q).Execute(ct);
        }

        /// <summary>
        /// KeyringUse is used to change the active gossip encryption key
        /// </summary>
        public Task<WriteResult> KeyringUse(string key, CancellationToken ct = default)
        {
            return KeyringUse(key, WriteOptions.Default, ct);
        }

        /// <summary>
        /// KeyringUse is used to change the active gossip encryption key
        /// </summary>
        public Task<WriteResult> KeyringUse(string key, WriteOptions q, CancellationToken ct = default)
        {
            return _client.Put("/v1/operator/keyring", new KeyringRequest() { Key = key }, q).Execute(ct);
        }

        public Task<QueryResult<ConsulLicense>> GetConsulLicense(string datacenter = "", CancellationToken ct = default)
        {
            return _client.Get<ConsulLicense>("/v1/operator/license", new QueryOptions { Datacenter = datacenter }).Execute(ct);
        }

        /// <summary>
        /// // SegmentList returns all the available LAN segments.
        /// </summary>
        public Task<QueryResult<string[]>> SegmentList(QueryOptions q, CancellationToken ct = default)
        {
            return _client.Get<string[]>("/v1/operator/segment", q).Execute(ct);
        }

        /// <summary>
        /// // SegmentList returns all the available LAN segments.
        /// </summary>
        public Task<QueryResult<string[]>> SegmentList(CancellationToken ct = default)
        {
            return SegmentList(QueryOptions.Default, ct);
        }

        /// <summary>
        /// CreateArea will create a new network area, a generated ID will be returned on success.
        /// </summary>
        public Task<WriteResult<string>> AreaCreate(AreaRequest area, CancellationToken ct = default)
        {
            return AreaCreate(area, WriteOptions.Default, ct);
        }

        /// <summary>
        /// CreateArea will create a new network area, a generated ID will be returned on success.
        /// </summary>
        public async Task<WriteResult<string>> AreaCreate(AreaRequest area, WriteOptions q, CancellationToken ct = default)
        {
            var req = await _client.Post<AreaRequest, Area>("/v1/operator/area", area, q).Execute(ct).ConfigureAwait(false);
            return new WriteResult<string>(req, req.Response.ID);
        }

        /// <summary>
        /// AreaList returns all the available network areas
        /// </summary>
        public Task<QueryResult<List<Area>>> AreaList(CancellationToken ct = default)
        {
            return AreaList(QueryOptions.Default, ct);
        }

        /// <summary>
        /// AreaList returns all the available network areas
        /// </summary>
        public Task<QueryResult<List<Area>>> AreaList(QueryOptions q, CancellationToken ct = default)
        {
            return _client.Get<List<Area>>("/v1/operator/area", q).Execute(ct);
        }
        /// <summary>
        /// AreaUpdate will update the configuration of the network area with the given area Id.
        /// </summary>
        public Task<WriteResult<string>> AreaUpdate(AreaRequest area, string areaId, CancellationToken ct = default)
        {
            return AreaUpdate(area, areaId, WriteOptions.Default, ct);
        }
        /// <summary>
        /// AreaUpdate will update the configuration of the network area with the given area Id.
        /// </summary>
        public async Task<WriteResult<string>> AreaUpdate(AreaRequest area, string areaId, WriteOptions q, CancellationToken ct = default)
        {
            var req = await _client.Put<AreaRequest, Area>($"/v1/operator/area/{areaId}", area, q).Execute(ct).ConfigureAwait(false);
            return new WriteResult<string>(req, req.Response.ID);
        }
        /// <summary>
        /// AreaGet returns a single network area
        /// </summary>
        public Task<QueryResult<Area[]>> AreaGet(string areaId, CancellationToken ct = default)
        {
            return AreaGet(areaId, QueryOptions.Default, ct);
        }
        /// <summary>
        /// AreaGet returns a single network area
        /// </summary>
        public Task<QueryResult<Area[]>> AreaGet(string areaId, QueryOptions q, CancellationToken ct = default)
        {
            return _client.Get<Area[]>($"/v1/operator/area/{areaId}", q).Execute(ct);
        }
        /// <summary>
        /// AreaDelete deletes the given network area.
        /// </summary>
        public Task<WriteResult> AreaDelete(string areaId, CancellationToken ct = default)
        {
            return AreaDelete(areaId, WriteOptions.Default, ct);
        }
        /// <summary>
        /// AreaDelete deletes the given network area.
        /// </summary>
        public Task<WriteResult> AreaDelete(string areaId, WriteOptions q, CancellationToken ct = default)
        {
            return _client.Delete($"/v1/operator/area/{areaId}", q).Execute(ct);
        }
        /// <summary>
        /// AreaJoin attempts to join the given set of join addresses to the given
        /// network area. See the Area structure for details about join addresses.
        /// </summary>
        public Task<WriteResult<List<AreaJoinResponse>>> AreaJoin(string areaId, List<string> addresses, CancellationToken ct = default)
        {
            return AreaJoin(areaId, addresses, WriteOptions.Default, ct);
        }
        /// <summary>
        /// AreaJoin attempts to join the given set of join addresses to the given
        /// network area. See the Area structure for details about join addresses.
        /// </summary>
        public Task<WriteResult<List<AreaJoinResponse>>> AreaJoin(string areaId, List<string> addresses, WriteOptions q, CancellationToken ct = default)
        {
            return _client.Put<List<string>, List<AreaJoinResponse>>($"/v1/operator/area/{areaId}/join", addresses, q).Execute(ct);
        }
    }

    public class ConsulLicense
    {
        public bool Valid { get; set; }
        public License License { get; set; }
        public string[] Warnings { get; set; }
    }

    public class License
    {
        [JsonProperty("license_id")]
        public string LicenseId { get; set; }
        [JsonProperty("customer_id")]
        public string CustomerId { get; set; }
        [JsonProperty("installation_id")]
        public string InstallationId { get; set; }

        [JsonProperty("issue_time")]
        public string IssueTime { get; set; }
        [JsonProperty("start_time")]
        public string StartTime { get; set; }
        [JsonProperty("expiration_time")]
        public string ExpirationTime { get; set; }
        public string Product { get; set; }
        public Flags Flags { get; set; }
        public string[] Features { get; set; }
        public bool Temporary { get; set; }
    }

    public class Flags
    {
        public string Package { get; set; }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Lazy<Operator> _operator;

        /// <summary>
        /// Operator returns a handle to the operator endpoints.
        /// </summary>
        public IOperatorEndpoint Operator => _operator.Value;
    }
}
