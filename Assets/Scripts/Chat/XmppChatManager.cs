using System;
using UnityEngine;
using Xmpp;
using Xmpp.protocol.iq.roster;
using Xmpp.protocol.client;
using System.Collections.Generic;
using TMPro;
using Assets.Scripts;
using System.Security.Cryptography;
using Unity.VisualScripting;
using System.Linq;

public class XmppChatManager : MonoBehaviour
{
    #region << Class's Attributes >>
    /************************************************************************
     * Class's attributes
     ************************************************************************/

    // ----- Bindable attributes  
    [SerializeField]
    private Transform ChatBoardContent;

    [SerializeField]
    private GameObject ChatItemObj;

    [SerializeField]
    private Transform FriendsListContent;

    [SerializeField]
    private GameObject FriendItemObj;

    [SerializeField]
    private TextMeshProUGUI ChatWithText;

    [SerializeField]
    private TMP_InputField UsernameInput;

    [SerializeField]
    private TMP_InputField PasswordInput;

    [SerializeField]
    private TMP_InputField ChatInput;

    // ----- Attributes in regards to personal friends  
    private bool renderPersonalFriendsList = false;

    private int lastTotalPersonalFriends = 0;
    private static List<Presence> friendPresenceUpdates;
    private static LinkedList<RosterItem> personalFriends;

    // ----- Attributes in regards to private chatting
    private bool renderOnScreenPrivateConversation = false;

    private string selectedFriendJid = "";
    private string onScreenFriendJid = "";
    private PrivateConversation onScreenPrivateConversation;

    private static List<string> friendsWhoHasNewMessage;
    private static List<Message> incomingPrivateMessages;
    private static Dictionary<string, LinkedList<Message>> privateConversations;

    // ----- Attributes related to the connection to the server 
    private static XmppClientConnection conn;

    private static int serv_port = Config.SERVER_PORT;
    private static string serv_url = Config.SERVER_URL;
    private static string username = "aqil_gifary_aqigif_gmail_com";
    private static string password = "555555";
    #endregion

    #region << MonoBehavior common functions >>
    /************************************************************************
     * MonoBehavior common functions
     ************************************************************************/

    // ----- Start is called before the first frame update 
    void Start()
    {
        if (friendPresenceUpdates == null || privateConversations == null ||
            incomingPrivateMessages == null || personalFriends == null ||
            friendsWhoHasNewMessage == null)
        {
            // ----- Initiate all static attributes
            privateConversations = new Dictionary<string, LinkedList<Message>>();
            personalFriends = new LinkedList<RosterItem>();
            incomingPrivateMessages = new List<Message>();
            friendsWhoHasNewMessage = new List<string>();
            friendPresenceUpdates = new List<Presence>();
        }
        if (conn == null)
        {
            // ----- Setting xmpp connection
            conn = new XmppClientConnection();
            conn.Status = PresenceType.available.ToString();
            conn.Show = ShowType.chat;
            conn.AutoPresence = true;
            conn.AutoRoster = true;
            conn.EnableCapabilities = true;

            // ----- Setting xmpp related event handlers
            conn.OnLogin += Conn_OnLogin;
            conn.OnError += Conn_OnError;
            conn.OnRosterStart += Conn_OnRosterStart;
            conn.OnRosterItem += Conn_OnRosterItem;
            conn.OnRosterEnd += Conn_OnRosterEnd;
            conn.OnPresence += Conn_OnPresence;
            conn.OnMessage += Conn_OnMessage;
        }
    }

    // ----- Update is called once per frame
    void Update()
    {
        // ----- Rendering friends list
        if (renderPersonalFriendsList)
        {
            int totalPersonalFriends = personalFriends.Count;
            if (totalPersonalFriends > lastTotalPersonalFriends)
            {
                foreach (RosterItem roster in personalFriends)
                {
                    if (!roster.Jid.ToString().Contains("conference"))
                    {
                        GameObject friendItem = Instantiate(FriendItemObj, transform);
                        FriendItem friendItemObj = friendItem.GetComponent<FriendItem>();
                        friendItemObj.Name = Util.ParseProfileFromJid(roster.Jid.Bare).FirstName;
                        friendItemObj.Jid = roster.Jid.ToString();
                        friendItemObj.Status = "Offline";
                        lastTotalPersonalFriends += 1;
                        friendItem.transform.SetParent(FriendsListContent, false);
                    }
                }
            }
            renderPersonalFriendsList = false;
        }

        // ----- Rendering friend's presence status
        if (friendPresenceUpdates != null && friendPresenceUpdates.Count > 0)
        {
            for (int a = 0; a < friendPresenceUpdates.Count; a++)
            {
                Presence pres = friendPresenceUpdates[a];
                string jid = pres.From.ToString().Split("/")[0];
                for (int i = 0; i < FriendsListContent.childCount; i++)
                {
                    GameObject friendItem = FriendsListContent.GetChild(i).gameObject;
                    if (friendItem != null)
                    {
                        FriendItem friendItemObj = friendItem.GetComponent<FriendItem>();
                        if (friendItemObj != null && friendItemObj.Jid == jid)
                        {
                            switch (pres.Type)
                            {
                                case PresenceType.available:
                                    friendItemObj.Status = "Online";
                                    break;
                                case PresenceType.unavailable:
                                    friendItemObj.Status = "Offline";
                                    break;
                                case PresenceType.error:
                                    friendItemObj.Status = "Offline";
                                    break;
                                case PresenceType.invisible:
                                    friendItemObj.Status = "Offline";
                                    break;
                            }
                        }
                    }
                }
            }
            friendPresenceUpdates.Clear();
        }

        // ----- Storing incoming private message
        if (incomingPrivateMessages != null && incomingPrivateMessages.Count > 0)
        {
            foreach (Message msg in incomingPrivateMessages)
            {
                string jid = msg.From.Bare;
                if (privateConversations.ContainsKey(jid))
                {
                    // ----- Updates cached conversations for received messages
                    LinkedList<Message> oldPrivateConversation = privateConversations.GetValueOrDefault(jid);
                    oldPrivateConversation.AddLast(msg);
                }
                else
                {
                    // ----- or create a new cache of conversation
                    LinkedList<Message> newPrivateConversation = new LinkedList<Message>();
                    newPrivateConversation.AddLast(msg);
                    privateConversations.Add(jid, newPrivateConversation);
                }
                updateOnScreenPrivateConversationOrUpdateSenderStatus(jid, Show.IF_NECESSARY);
            }
            incomingPrivateMessages.Clear();
        }

        // ----- Change friend's status if has message
        if (friendsWhoHasNewMessage != null && friendsWhoHasNewMessage.Count > 0)
        {
            for (int x = 0; x < friendsWhoHasNewMessage.Count; x++)
            {
                string jid = friendsWhoHasNewMessage[x];
                for (int i = 0; i < FriendsListContent.childCount; i++)
                {
                    GameObject friendItem = FriendsListContent.GetChild(i).gameObject;
                    if (friendItem != null)
                    {
                        FriendItem friendItemObj = friendItem.GetComponent<FriendItem>();
                        if (friendItemObj != null && friendItemObj.Jid == jid)
                        {
                            friendItemObj.HasMessage = true;
                        }
                    }
                }
            }
            friendsWhoHasNewMessage.Clear();
        }

        // ----- Change 'Chat with' label and switch current on-screen conversation
        if (selectedFriendJid != onScreenFriendJid)
        {
            if (!privateConversations.ContainsKey(selectedFriendJid))
            {
                // ----- Create a new cache of conversation
                LinkedList<Message> newPrivateConversation = new LinkedList<Message>();
                privateConversations.Add(selectedFriendJid, newPrivateConversation);
            }
            onScreenFriendJid = selectedFriendJid;
            UserProfile profile = Util.ParseProfileFromJid(selectedFriendJid);
            setChatWithLabel(ChatWithText, profile);
            updateOnScreenPrivateConversationOrUpdateSenderStatus(onScreenFriendJid, Show.MUST_SHOW);
        }

        // ----- Rendering a private conversation to screen
        if (renderOnScreenPrivateConversation)
        {
            if (onScreenPrivateConversation != null)
            {
                UserProfile profile = Util.ParseProfileFromJid(onScreenPrivateConversation.Jid);
                setChatWithLabel(ChatWithText, profile);
                // ----- Destroying all gameobjects in scroll view content
                if (ChatBoardContent.childCount > 0)
                {
                    while (ChatBoardContent.childCount > 0)
                    {
                        DestroyImmediate(ChatBoardContent.GetChild(0).gameObject);
                    }
                }
                // ----- Recreate gameobjects in scroll view content
                foreach (Message msg in onScreenPrivateConversation.Messages)
                {
                    GameObject chatItem = Instantiate(ChatItemObj, transform);
                    ConversationItem conversationItemObj = chatItem.GetComponent<ConversationItem>();
                    conversationItemObj.SenderJid = msg.From.Bare;
                    conversationItemObj.SenderName = Util.ParseProfileFromJid(msg.From.Bare).FirstName;
                    conversationItemObj.Message = msg.Body;
                    chatItem.transform.SetParent(ChatBoardContent, false);
                }
            }
            renderOnScreenPrivateConversation = false;
        }
    }

    // ----- Functions to handle on application closed
    void OnApplicationQuit()
    {
        if (conn != null && conn.Authenticated)
        {
            conn.Close();
        }
    }
    #endregion

    #region << Events handlers functions >>
    /************************************************************************
     * User events handlers functions
     ************************************************************************/
    public void OnLoginBtnClicked()
    {
        if (conn != null && !conn.Authenticated)
        {
            conn.Port = serv_port;
            conn.Server = serv_url;
            conn.Open(username, password);
        }
    }

    public void OnSendBtnClicked()
    {
        if (conn != null && conn.Authenticated)
        {
            if (onScreenPrivateConversation != null)
            {
                if (ChatInput.text.Length > 0)
                {
                    string message = ChatInput.text;
                    Jid jid = new Jid(onScreenPrivateConversation.Jid);
                    Message chatMsg = new Message(jid, conn.MyJID);
                    chatMsg.Body = message;
                    chatMsg.Type = MessageType.chat;
                    conn.Send(chatMsg);
                    addMessageToOnScreenPrivateConversation(jid.Bare, chatMsg);
                    ChatInput.text = "";
                }
            }
        }
    }

    public void SetSelectedJid(string jid)
    {
        if (jid != onScreenFriendJid)
        {
            selectedFriendJid = jid;
        }
    }

    /************************************************************************
     * XMPP events handlers functions
     ************************************************************************/
    // ----- Handles incoming Message, currently is in use to receives private chat
    private void Conn_OnMessage(object sender, Message msg)
    {
        if (msg.Body != null && msg.Body.Length > 0)
        {
            incomingPrivateMessages.Add(msg);
        }
    }

    // ----- Handles incoming Presence message, currently is in use for getting
    // ----- updates on friend's availability (on/offline)
    private void Conn_OnPresence(object sender, Presence pres)
    {
        friendPresenceUpdates.Add(pres);
    }

    // ----- Handles incoming Roster message, to notify this chat app that
    // ----- the server will start sending friends data
    private void Conn_OnRosterStart(object sender)
    {
        renderPersonalFriendsList = false;
    }

    // ----- Handles incoming Roster (friend's data) so this app can
    // ----- stores it to a collection object
    private void Conn_OnRosterItem(object sender, RosterItem item)
    {
        personalFriends.AddLast(item);
    }

    // ----- Handles incoming Roster message, to notify this chat app that
    // ----- the server has finished sending all his/her friends data
    private void Conn_OnRosterEnd(object sender)
    {
        renderPersonalFriendsList = true;
    }

    // ----- Handles incoming notification that the user successfully
    // ----- logged in into the server
    private void Conn_OnLogin(object sender)
    {
        // ----- Login succeed
    }

    private void Conn_OnError(object sender, Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
    #endregion

    #region << Helper functions >>
    /************************************************************************
     * Helper functions
     ************************************************************************/
    private void addMessageToOnScreenPrivateConversation(string jid, Message msg)
    {
        if (privateConversations.ContainsKey(jid))
        {
            LinkedList<Message> oldPrivateConversation = privateConversations.GetValueOrDefault(jid);
            oldPrivateConversation.AddLast(msg);
            updateOnScreenPrivateConversationOrUpdateSenderStatus(jid, Show.MUST_SHOW);
            renderOnScreenPrivateConversation = true;
        }
    }

    private void updateOnScreenPrivateConversationOrUpdateSenderStatus(string jid, Show show)
    {
        // ----- Checks if there is an on-screen conversation currently
        // ----- if there isn't any, create one and display it on-screen
        if (onScreenPrivateConversation != null)
        {
            if (onScreenPrivateConversation.Jid == jid)
            {
                // ----- Updates current on-screen conversation if the sender jid equals to
                // ----- the current on-screen conversation jid
                repopulateOnScreenPrivateConversation(jid);
            }
            else
            {
                switch (show)
                {
                    case Show.MUST_SHOW:
                        // ----- Switch on-screen conversation to the conversation with
                        // ----- sender jid
                        repopulateOnScreenPrivateConversation(jid);
                        break;
                    case Show.IF_NECESSARY:
                        // ----- Add 'MSG' tag in sender's roster in friends list
                        friendsWhoHasNewMessage.Add(jid);
                        break;
                }
            }
        }
        else
        {
            // ----- Creating a new on-screen conversation obj since there is no
            // ----- on-screen conversation currently
            onScreenPrivateConversation = new PrivateConversation(jid);
            // ----- Populate current private conversation from cached conversation
            repopulateOnScreenPrivateConversation(jid);
        }
    }

    private void repopulateOnScreenPrivateConversation(string jid)
    {
        selectedFriendJid = onScreenFriendJid = jid;
        onScreenPrivateConversation.Jid = jid;
        onScreenPrivateConversation.Messages.Clear();
        LinkedList<Message> oldPrivateConversation = privateConversations.GetValueOrDefault(jid);
        foreach (Message o in oldPrivateConversation.ToArray())
        {
            onScreenPrivateConversation.Messages.AddLast(o);
        }
        renderOnScreenPrivateConversation = true;
    }

    private void setChatWithLabel(TextMeshProUGUI component, UserProfile profile)
    {
        component.text = "Chat with: " + profile.FirstName + " " + profile.LastName;
    }
    #endregion
}
