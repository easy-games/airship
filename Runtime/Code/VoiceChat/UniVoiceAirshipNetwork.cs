using System;
using System.Collections.Generic;
using Adrenak.UniVoice;

namespace Code.VoiceChat {
    public class UniVoiceAirshipNetwork : IChatroomNetwork {
        public void Dispose() {
            throw new NotImplementedException();
        }

        public event Action OnCreatedChatroom;
        public event Action<Exception> OnChatroomCreationFailed;
        public event Action OnClosedChatroom;
        public event Action<short> OnJoinedChatroom;
        public event Action<Exception> OnChatroomJoinFailed;
        public event Action OnLeftChatroom;
        public event Action<short> OnPeerJoinedChatroom;
        public event Action<short> OnPeerLeftChatroom;
        public event Action<short, ChatroomAudioSegment> OnAudioReceived;
        public event Action<short, ChatroomAudioSegment> OnAudioSent;
        public short OwnID { get; }
        public List<short> PeerIDs { get; }
        public void HostChatroom(object data = null) {
            throw new NotImplementedException();
        }

        public void CloseChatroom(object data = null) {
            throw new NotImplementedException();
        }

        public void JoinChatroom(object data = null) {
            throw new NotImplementedException();
        }

        public void LeaveChatroom(object data = null) {
            throw new NotImplementedException();
        }

        public void SendAudioSegment(short peerID, ChatroomAudioSegment data) {
            throw new NotImplementedException();
        }
    }
}