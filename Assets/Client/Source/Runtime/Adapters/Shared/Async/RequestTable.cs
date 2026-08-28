using System.Collections.Generic;

namespace Client.Adapters.Shared.Async
{
    /// <summary>
    /// The request bookkeeping every asynchronous adapter needs: mint an id, find the request
    /// behind it, drop it. One copy instead of the one each adapter used to hand-roll.
    /// </summary>
    /// <remarks>
    /// Ids start at 1, so <c>0</c> is never a live request and stays free as the "no request"
    /// sentinel every component uses. Ids are never reused — a released id reads as unknown
    /// forever, which is what lets <c>Poll</c> answer Pending instead of throwing.
    /// <para>The table holds the adapter's own request record, whatever that is: a web transport,
    /// an Addressables handle, a cancellation token. It knows nothing about status, which is why
    /// there is one of these and not one per port.</para>
    /// </remarks>
    /// <typeparam name="TRequest">The adapter's request record. A class where the adapter
    /// updates the record it just looked up; a readonly struct where it only reads one.</typeparam>
    public sealed class RequestTable<TRequest>
    {
        private readonly Dictionary<int, TRequest> _requests = new();
        private int _nextRequestId;

        /// <summary>Requests started and not yet released. Must be 0 after a clean shutdown.</summary>
        public int Count => _requests.Count;

        /// <summary>Every live request, for a teardown that has to release them all.</summary>
        public Dictionary<int, TRequest>.ValueCollection Values => _requests.Values;

        /// <summary>Files the request and returns the id that names it from here on.</summary>
        public int Add(TRequest request)
        {
            var requestId = ++_nextRequestId;
            _requests.Add(requestId, request);
            return requestId;
        }

        /// <summary>The request behind an id, or false when the id is unknown or released.</summary>
        public bool TryGet(int requestId, out TRequest request) =>
            _requests.TryGetValue(requestId, out request);

        /// <summary>Drops the request and hands it back so the caller can free what it held.</summary>
        public bool Remove(int requestId, out TRequest request) =>
            _requests.Remove(requestId, out request);

        /// <summary>Forgets every request. The caller frees what they held first.</summary>
        public void Clear() => _requests.Clear();
    }
}
