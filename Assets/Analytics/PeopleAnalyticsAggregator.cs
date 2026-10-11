using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PeopleAnalyticsAggregator : NetworkBehaviour
{
    public static PeopleAnalyticsAggregator Instance { get; private set; }

    public struct ParticipantStat : IEquatable<ParticipantStat>, INetworkSerializable
    {
        public ulong clientId;
        public FixedString64Bytes name;
        public float talkSeconds;
        public int turns;

        public bool Equals(ParticipantStat other) => clientId == other.clientId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref name);
            serializer.SerializeValue(ref talkSeconds);
            serializer.SerializeValue(ref turns);
        }
    }

    public NetworkList<ParticipantStat> Stats;

    [Header("Engagement (owner computes)")]
    public float windowSeconds = 30f;
    public float lowEngagementThresholdTalkSeconds = 1.5f;

    public NetworkVariable<bool> IsLowEngagement = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    float windowTimer;
    float windowTalkAccum;

    void Awake()
    {
        Instance = this;
        Stats ??= new NetworkList<ParticipantStat>();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PeopleAnalyticsAgg] OnNetworkSpawn IsOwner={IsOwner} IsServer={IsServer} IsClient={IsClient}");

        // רק מי שבעלים מאתחל וסופר
        if (!IsOwner) return;

        Stats.Clear();
        windowTimer = 0f;
        windowTalkAccum = 0f;
        IsLowEngagement.Value = false;
    }

    void Update()
    {
        if (!IsOwner) return;

        windowTimer += Time.deltaTime;
        if (windowTimer >= windowSeconds)
        {
            windowTimer = 0f;
            IsLowEngagement.Value = windowTalkAccum < lowEngagementThresholdTalkSeconds;
            windowTalkAccum = 0f;
        }
    }

    public void AddDelta(ulong clientId, FixedString64Bytes displayName, float talkDelta, int turnsDelta)
    {
        if (!IsOwner) return;

        float td = Mathf.Max(0f, talkDelta);
        int trn = Mathf.Max(0, turnsDelta);

        windowTalkAccum += td;

        int index = FindIndex(clientId);
        if (index < 0)
        {
            Stats.Add(new ParticipantStat
            {
                clientId = clientId,
                name = displayName.Length > 0 ? displayName : new FixedString64Bytes($"Player {clientId}"),
                talkSeconds = td,
                turns = trn
            });
        }
        else
        {
            var st = Stats[index];
            if (displayName.Length > 0) st.name = displayName;
            st.talkSeconds += td;
            st.turns += trn;
            Stats[index] = st;
        }
    }

    int FindIndex(ulong clientId)
    {
        for (int i = 0; i < Stats.Count; i++)
            if (Stats[i].clientId == clientId) return i;
        return -1;
    }
}
