using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hackbox
{
    [RequireComponent(typeof(Host))]
    public class GameHost : MonoBehaviour
    {
        [Tooltip("Called as soon as the game is started.")]
        public UnityEvent OnGameStarted = new UnityEvent();

        [SerializeField] private GameObject panelConnecting, panelLobby, panelIntro, panelStart, panelGameplayDefault, panelVotePending, panelVoteFailed, panelVoteIncorrect, panelVoteCorrect, panelDisconnected, panelWinner, panelGameOver, lastFalseAccusation, notLastFalseAccusation, team1Wins, team2Wins, doubleAgentWins;
        [SerializeField] private GameObject[] minPlayersNotJoined, minPlayersJoined;

        [SerializeField] private TextMeshProUGUI roomCodeText, lobbyCapacityText, countdownText;
        [SerializeField] private TextMeshProUGUI[] lobbyWaitingText, lobbyPlayerNameText, accuseeText;
        
        [SerializeField] private StateAsset statePlayerLobby, stateAudience, stateIdle, stateStartTeam1, stateStartTeam2, stateStartDoubleAgent, stateAccused, stateVoted, stateWin, stateLose, stateDisconnected;
        [SerializeField] private Hackbox.UI.Theme themeTeam1, themeTeam2, themeDoubleAgent;
        [SerializeField] private Hackbox.UI.Preset textHeadingPreset, textBodyPreset, buttonPreset;

        [SerializeField] private int playerCountPerTeam;

        private Host host;

        private Member[] players, audienceMembers;
        private bool hideRoomCode, enableAudience, enableMaturePrompts, gameHasStarted, disconnectedOnce, disconnected, paused, pAccusationTeam1, pAccusationTeam2, firstRound;
        private bool[] voted;
        private int playerTurn, accusee, votes, falseConvictions;
        private int[] playerRoles, playerOrder;
        private string[] logs1, logs2;

        void Awake()
        {
            host = GetComponent<Host>();
        }

        // Start is called before the first frame update
        void Start()
        {
            host.Disconnect();
            host.Connect(true);
            players = new Member[0];
            lobbyCapacityText.text = "Waiting for " + (playerCountPerTeam * 2 + 1 == 1 ? "1 more player..." : (playerCountPerTeam * 2 + 1) + " more players...");
        }

        // Update is called once per frame
        void Update()
        {
            
        }

        private void OnEnable()
        {
            host.OnRoomConnected.AddListener(OnRoomConnected);
            host.OnRoomDisconnected.AddListener(OnRoomDisconnected);
            host.OnMemberJoined.AddListener(OnMemberJoined);
            host.OnMessage.AddListener(OnMessage);
        }

        private void OnDisable()
        {
            host.OnRoomConnected.RemoveListener(OnRoomConnected);
            host.OnRoomDisconnected.AddListener(OnRoomDisconnected);
            host.OnMemberJoined.RemoveListener(OnMemberJoined);
            host.OnMessage.RemoveListener(OnMessage);
        }

        private void OnRoomConnected(string roomCode)
        {
            if (disconnectedOnce)
            {
                panelDisconnected.SetActive(false);
            }
            else
            {
                panelConnecting.SetActive(false);
                panelLobby.SetActive(true);
                roomCodeText.text = roomCode;
            }
            disconnected = false;
        }

        private void OnRoomDisconnected(string roomCode)
        {
            panelDisconnected.SetActive(true);
            disconnectedOnce = true;
            disconnected = true;
        }

        private void OnMemberJoined(Member member)
        {
            if (players.Length < playerCountPerTeam * 2 + 1 && !gameHasStarted)
            {
                Hackbox.State state = statePlayerLobby.State;
                state.SetHeaderText(member.Name);
                host.UpdateMemberState(member, state);
                lobbyWaitingText[players.Length].gameObject.SetActive(false);
                lobbyPlayerNameText[players.Length].text = member.Name;
                List<Member> playerList = new List<Member>(players);
                playerList.Add(member);
                players = playerList.ToArray();
                if (players.Length < playerCountPerTeam * 2 + 1) lobbyCapacityText.text = "Waiting for " + (playerCountPerTeam * 2 + 1 - players.Length == 1 ? "1 more player..." : (playerCountPerTeam * 2 + 1 - players.Length) + " more players...");
                else
                {
                    foreach (GameObject o in minPlayersNotJoined) o.SetActive(false);
                    foreach (GameObject o in minPlayersJoined) o.SetActive(true);
                    host.TwitchRequired = false;
                }
                lobbyCapacityText.text = players.Length + "/" + (playerCountPerTeam * 2 + 1);
            }
            else host.UpdateMemberState(member, stateAudience);
        }

        private void OnMessage(Message message)
        {
            Member member = null;
            int playerIndex = -1;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == message.Member)
                {
                    member = players[i];
                    playerIndex = i;
                }
            }
            if (member == null) return;
            Hackbox.State state;
            Hackbox.UI.UIComponent newComponent;
            Hackbox.UI.Theme theme;
            switch (message.Event)
            {
                case "SelectLog":
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    state.SetHeaderText(member.Name);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Choose a player to show your current log.");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (i != playerIndex)
                        {
                            newComponent = new Hackbox.UI.UIComponent()
                            {
                                Name = "button" + i,
                                Preset = buttonPreset,
                            };
                            newComponent.SetParameterValue<string>("label", players[i].Name);
                            newComponent.SetParameterValue<string>("event", "LogPlayer" + (i + 1));
                            state.Components.Add(newComponent);
                        }
                    }
                    host.UpdateMemberState(member, state);
                    break;
                case "SelectAccuse":
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    state.SetHeaderText(member.Name);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Choose a player to accuse.");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (i != playerIndex)
                        {
                            newComponent = new Hackbox.UI.UIComponent()
                            {
                                Name = "button" + i,
                                Preset = buttonPreset,
                            };
                            newComponent.SetParameterValue<string>("label", players[i].Name);
                            newComponent.SetParameterValue<string>("event", "AccusePlayer" + (i + 1));
                            state.Components.Add(newComponent);
                        }
                    }
                    host.UpdateMemberState(member, state);
                    votes = 0;
                    pAccusationTeam1 = playerRoles[playerIndex] == 0;
                    pAccusationTeam2 = playerRoles[playerIndex] == 1;
                    break;
                case "LogPlayer1":
                    logs1[0] += "," + logs1[playerIndex];
                    logs2[0] += "," + logs2[playerIndex];
                    state = stateIdle;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    StartTurn(true);
                    break;
                case "LogPlayer2":
                    logs1[1] += "," + logs1[playerIndex];
                    logs2[1] += "," + logs2[playerIndex];
                    state = stateIdle;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    StartTurn(true);
                    break;
                case "LogPlayer3":
                    logs1[2] += "," + logs1[playerIndex];
                    logs2[2] += "," + logs2[playerIndex];
                    state = stateIdle;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    StartTurn(true);
                    break;
                case "LogPlayer4":
                    logs1[3] += "," + logs1[playerIndex];
                    logs2[3] += "," + logs2[playerIndex];
                    state = stateIdle;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    StartTurn(true);
                    break;
                case "LogPlayer5":
                    logs1[4] += "," + logs1[playerIndex];
                    logs2[4] += "," + logs2[playerIndex];
                    state = stateIdle;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    StartTurn(true);
                    break;
                case "LogPlayer6":
                    logs1[5] += "," + logs1[playerIndex];
                    logs2[5] += "," + logs2[playerIndex];
                    state = stateIdle;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    StartTurn(true);
                    break;
                case "LogPlayer7":
                    logs1[6] += "," + logs1[playerIndex];
                    logs2[6] += "," + logs2[playerIndex];
                    state = stateIdle;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    StartTurn(true);
                    break;
                case "AccusePlayer1":
                    accusee = 0;
                    state = stateAccused;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    foreach (TextMeshProUGUI t in accuseeText) t.text = players[accusee].Name;
                    panelGameplayDefault.SetActive(false);
                    panelVotePending.SetActive(true);
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Someone on your team has just accused the following player: " + players[accusee].Name);
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text2",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Do you think this player is the double agent?  Vote now!");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button1",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Aye!");
                    newComponent.SetParameterValue<string>("event", "VoteAye");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button2",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Nay!");
                    newComponent.SetParameterValue<string>("event", "VoteNay");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (playerIndex != i && i != accusee && playerRoles[playerIndex] == playerRoles[i])
                        {
                            state.SetHeaderText(players[i].Name);
                            host.UpdateMemberState(players[i], state);
                        }
                    }
                    break;
                case "AccusePlayer2":
                    accusee = 1;
                    state = stateAccused;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    foreach (TextMeshProUGUI t in accuseeText) t.text = players[accusee].Name;
                    panelGameplayDefault.SetActive(false);
                    panelVotePending.SetActive(true);
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Someone on your team has just accused the following player: " + players[accusee].Name);
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text2",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Do you think this player is the double agent?  Vote now!");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button1",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Aye!");
                    newComponent.SetParameterValue<string>("event", "VoteAye");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button2",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Nay!");
                    newComponent.SetParameterValue<string>("event", "VoteNay");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (playerIndex != i && i != accusee && playerRoles[playerIndex] == playerRoles[i])
                        {
                            state.SetHeaderText(players[i].Name);
                            host.UpdateMemberState(players[i], state);
                        }
                    }
                    break;
                case "AccusePlayer3":
                    accusee = 2;
                    state = stateAccused;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    foreach (TextMeshProUGUI t in accuseeText) t.text = players[accusee].Name;
                    panelGameplayDefault.SetActive(false);
                    panelVotePending.SetActive(true);
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Someone on your team has just accused the following player: " + players[accusee].Name);
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text2",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Do you think this player is the double agent?  Vote now!");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button1",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Aye!");
                    newComponent.SetParameterValue<string>("event", "VoteAye");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button2",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Nay!");
                    newComponent.SetParameterValue<string>("event", "VoteNay");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (playerIndex != i && i != accusee && playerRoles[playerIndex] == playerRoles[i])
                        {
                            state.SetHeaderText(players[i].Name);
                            host.UpdateMemberState(players[i], state);
                        }
                    }
                    break;
                case "AccusePlayer4":
                    accusee = 3;
                    state = stateAccused;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    foreach (TextMeshProUGUI t in accuseeText) t.text = players[accusee].Name;
                    panelGameplayDefault.SetActive(false);
                    panelVotePending.SetActive(true);
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Someone on your team has just accused the following player: " + players[accusee].Name);
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text2",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Do you think this player is the double agent?  Vote now!");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button1",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Aye!");
                    newComponent.SetParameterValue<string>("event", "VoteAye");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button2",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Nay!");
                    newComponent.SetParameterValue<string>("event", "VoteNay");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (playerIndex != i && i != accusee && playerRoles[playerIndex] == playerRoles[i])
                        {
                            state.SetHeaderText(players[i].Name);
                            host.UpdateMemberState(players[i], state);
                        }
                    }
                    break;
                case "AccusePlayer5":
                    accusee = 4;
                    state = stateAccused;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    foreach (TextMeshProUGUI t in accuseeText) t.text = players[accusee].Name;
                    panelGameplayDefault.SetActive(false);
                    panelVotePending.SetActive(true);
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Someone on your team has just accused the following player: " + players[accusee].Name);
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text2",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Do you think this player is the double agent?  Vote now!");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button1",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Aye!");
                    newComponent.SetParameterValue<string>("event", "VoteAye");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button2",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Nay!");
                    newComponent.SetParameterValue<string>("event", "VoteNay");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (playerIndex != i && i != accusee && playerRoles[playerIndex] == playerRoles[i])
                        {
                            state.SetHeaderText(players[i].Name);
                            host.UpdateMemberState(players[i], state);
                        }
                    }
                    break;
                case "AccusePlayer6":
                    accusee = 5;
                    state = stateAccused;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    foreach (TextMeshProUGUI t in accuseeText) t.text = players[accusee].Name;
                    panelGameplayDefault.SetActive(false);
                    panelVotePending.SetActive(true);
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Someone on your team has just accused the following player: " + players[accusee].Name);
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text2",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Do you think this player is the double agent?  Vote now!");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button1",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Aye!");
                    newComponent.SetParameterValue<string>("event", "VoteAye");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button2",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Nay!");
                    newComponent.SetParameterValue<string>("event", "VoteNay");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (playerIndex != i && i != accusee && playerRoles[playerIndex] == playerRoles[i])
                        {
                            state.SetHeaderText(players[i].Name);
                            host.UpdateMemberState(players[i], state);
                        }
                    }
                    break;
                case "AccusePlayer7":
                    accusee = 6;
                    state = stateAccused;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    state.SetHeaderText(players[playerIndex].Name);
                    host.UpdateMemberState(players[playerIndex], state);
                    foreach (TextMeshProUGUI t in accuseeText) t.text = players[accusee].Name;
                    panelGameplayDefault.SetActive(false);
                    panelVotePending.SetActive(true);
                    state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]] };
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text1",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Someone on your team has just accused the following player: " + players[accusee].Name);
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "text2",
                        Preset = textHeadingPreset,
                    };
                    newComponent.SetParameterValue<string>("text", "Do you think this player is the double agent?  Vote now!");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button1",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Aye!");
                    newComponent.SetParameterValue<string>("event", "VoteAye");
                    state.Components.Add(newComponent);
                    newComponent = new Hackbox.UI.UIComponent()
                    {
                        Name = "button2",
                        Preset = buttonPreset,
                    };
                    newComponent.SetParameterValue<string>("label", "Nay!");
                    newComponent.SetParameterValue<string>("event", "VoteNay");
                    state.Components.Add(newComponent);
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (playerIndex != i && i != accusee && playerRoles[playerIndex] == playerRoles[i])
                        {
                            state.SetHeaderText(players[i].Name);
                            host.UpdateMemberState(players[i], state);
                        }
                    }
                    break;
                case "VoteAye":
                    votes++;
                    if (votes < playerCountPerTeam - (playerRoles[playerIndex] == playerRoles[accusee] ? 2 : 1))
                    {
                        state = stateVoted;
                        state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                        state.SetHeaderText(players[playerIndex].Name);
                        host.UpdateMemberState(players[playerIndex], state);
                    }
                    else
                    {
                        theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                        for (int i = 0; i < players.Length; i++)
                        {
                            if (playerRoles[playerIndex] == playerRoles[i])
                            {
                                state = stateIdle;
                                state.Theme = theme;
                                state.SetHeaderText(players[i].Name);
                                host.UpdateMemberState(players[i], state);
                            }
                        }
                        panelVotePending.SetActive(false);
                        (playerRoles[accusee] == 2 ? panelVoteCorrect : panelVoteIncorrect).SetActive(true);
                        lastFalseAccusation.SetActive(falseConvictions == 1);
                        notLastFalseAccusation.SetActive(falseConvictions == 0);
                    }
                    break;
                case "VoteNay":
                    theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerIndex]];
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (playerRoles[playerIndex] == playerRoles[i])
                        {
                            state = stateIdle;
                            state.Theme = theme;
                            state.SetHeaderText(players[i].Name);
                            host.UpdateMemberState(players[i], state);
                        }
                    }
                    panelVotePending.SetActive(false);
                    panelVoteFailed.SetActive(true);
                    break;
            }
        }

        public void StartGame()
        {
            StartCoroutine(StartGameCountdown());
        }

        public void CancelStartGame()
        {
            StopAllCoroutines();
            countdownText.text = "";
        }

        private IEnumerator StartGameCountdown()
        {
            for (int i = 0; i < 3; i++)
            {
                while (disconnected || paused) yield return null;
                countdownText.text = (3 - i) + "";
                yield return new WaitForSeconds(1f);
            }
            while (disconnected || paused) yield return null;
            panelLobby.SetActive(false);
            panelGameOver.SetActive(false);
            countdownText.text = "";
            foreach (Member player in players)
            {
                Hackbox.State state = stateIdle;
                state.Theme = themeDoubleAgent;
                state.SetHeaderText(player.Name);
                host.UpdateMemberState(player, state);
            }
            BeginGame();
        }

        private void BeginGame()
        {
            gameHasStarted = true;
            playerTurn = 0;
            firstRound = true;
            falseConvictions = 0;
            OnGameStarted.Invoke();
        }

        public void AssignRoles()
        {
            playerOrder = new int[players.Length];
            bool[] indices = new bool[players.Length];
            int r;
            for (int i = 0; i < players.Length; i++)
            {
                do r = Random.Range(0, players.Length);
                while (indices[r]);
                playerOrder[i] = r;
                indices[r] = true;
            }
            playerRoles = new int[players.Length];
            indices = new bool[players.Length];
            for (int i = 0; i < playerCountPerTeam; i++)
            {
                do r = Random.Range(0, players.Length);
                while (indices[r]);
                playerRoles[r] = 1;
                indices[r] = true;
            }
            do r = Random.Range(0, players.Length);
            while (indices[r]);
            playerRoles[r] = 2;
            logs1 = new string[players.Length];
            logs2 = new string[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                logs1[i] = i + "";
                logs2[i] = i + "";
                Hackbox.State state = (new Hackbox.State[]{ stateStartTeam1, stateStartTeam2, stateStartDoubleAgent })[playerRoles[i]];
                state.SetHeaderText(players[i].Name);
                host.UpdateMemberState(players[i], state);
            }
        }

        public void StartTurn(bool next)
        {
            if (next)
            {
                playerTurn = (playerTurn + 1) % players.Length;
                if (playerTurn == 0) firstRound = false;
            }
            Hackbox.State state = new Hackbox.State() { Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[playerOrder[playerTurn]]] };
            Hackbox.UI.UIComponent newComponent = new Hackbox.UI.UIComponent()
            {
                Name = "text1",
                Preset = textHeadingPreset,
            };
            newComponent.SetParameterValue<string>("text", "It's your turn!  Select an action to take.");
            state.Components.Add(newComponent);
            newComponent = new Hackbox.UI.UIComponent()
            {
                Name = "button1",
                Preset = buttonPreset,
            };
            newComponent.SetParameterValue<string>("label", "Give your log to another player");
            newComponent.SetParameterValue<string>("event", "SelectLog");
            state.Components.Add(newComponent);
            if (!firstRound && (playerRoles[playerOrder[playerTurn]] == 0 && !pAccusationTeam1 || playerRoles[playerOrder[playerTurn]] == 1 && !pAccusationTeam2))
            {
                newComponent = new Hackbox.UI.UIComponent()
                {
                    Name = "button2",
                    Preset = buttonPreset,
                };
                newComponent.SetParameterValue<string>("label", "Make an accusation");
                newComponent.SetParameterValue<string>("event", "SelectAccuse");
                state.Components.Add(newComponent);
            }
            string[] newLogs1 = logs1[playerOrder[playerTurn]].Split(',');
            string[] newLogs2 = logs2[playerOrder[playerTurn]].Split(',');
            string nLogs1 = "";
            string nLogs2 = "";
            for (int i = 0; i < newLogs1.Length; i++)
            {
                int log1 = int.Parse(newLogs1[i]);
                int log2 = int.Parse(newLogs2[i]);
                bool redundant = false;
                for (int j = 0; j < i; j++) redundant |= playerOrder[playerTurn] == log1 && playerOrder[playerTurn] + "" == newLogs1[j] || newLogs1[i] == newLogs1[j] && newLogs2[i] == newLogs2[j] || newLogs1[i] == newLogs2[j] && newLogs1[j] == newLogs2[i];
                newComponent = new Hackbox.UI.UIComponent()
                {
                    Name = "text" + (i + 2),
                    Preset = textHeadingPreset,
                };
                string log;
                if (playerOrder[playerTurn] == log1)
                {
                    if (!redundant)
                    {
                        nLogs1 += i == 0 ? log1 + "" : "," + log1;
                        nLogs2 += i == 0 ? log2 + "" : "," + log2;
                    }
                    log = "You are " + (new string[]{ "on the red team.", "on the blue team.", "the double agent." })[playerRoles[playerOrder[playerTurn]]];
                }
                else if (playerOrder[playerTurn] == log2 || log1 == log2)
                {
                    if (!redundant)
                    {
                        for (int j = 0; j < i; j++) redundant |= newLogs1[i] == newLogs1[j] && playerOrder[playerTurn] + "" == newLogs2[j] || newLogs1[i] == newLogs2[j] && newLogs1[j] == playerOrder[playerTurn] + "";
                        if (!redundant)
                        {
                            nLogs1 += i == 0 ? log1 + "" : "," + log1;
                            nLogs2 += i == 0 ? playerOrder[playerTurn] + "" : "," + playerOrder[playerTurn];
                        }
                    }
                    log = players[log1].Name + " is " + (playerRoles[log1] == 2 || playerRoles[playerOrder[playerTurn]] == playerRoles[log1] ? "" : "not") + " on your team.";
                }
                else
                {
                    if (!redundant)
                    {
                        nLogs1 += i == 0 ? log1 + "" : "," + log1;
                        nLogs2 += i == 0 ? log2 + "" : "," + log2;
                    }
                    log = players[log1].Name + " is " + (playerRoles[log1] == playerRoles[log2] ? "" : "not ") + "on " + players[log2].Name + "'s team.";
                }
                newComponent.SetParameterValue<string>("text", log);
                if (!redundant) state.Components.Add(newComponent);
            }
            logs1[playerOrder[playerTurn]] = nLogs1;
            logs2[playerOrder[playerTurn]] = nLogs2;
            state.SetHeaderText(players[playerOrder[playerTurn]].Name);
            host.UpdateMemberState(players[playerOrder[playerTurn]], state);
        }

        public void CheckGameOver()
        {
            if (playerRoles[accusee] == 2)
            {
                panelWinner.SetActive(true);
                if (playerRoles[playerOrder[playerTurn]] == 0) team1Wins.SetActive(true);
                else if (playerRoles[playerOrder[playerTurn]] == 1) team2Wins.SetActive(true);
                for (int i = 0; i < players.Length; i++)
                {
                    Hackbox.State state = playerRoles[playerOrder[playerTurn]] == playerRoles[i] ? stateWin : stateLose;
                    state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[i]];
                    state.SetHeaderText(players[i].Name);
                    host.UpdateMemberState(players[i], state);
                }
            }
            else
            {
                falseConvictions++;
                if (falseConvictions < 2)
                {
                    panelGameplayDefault.SetActive(true);
                    StartTurn(true);
                }
                else
                {
                    panelWinner.SetActive(true);
                    doubleAgentWins.SetActive(true);
                    for (int i = 0; i < players.Length; i++)
                    {
                        Hackbox.State state = playerRoles[i] == 2 ? stateWin : stateLose;
                        state.Theme = (new Hackbox.UI.Theme[]{ themeTeam1, themeTeam2, themeDoubleAgent })[playerRoles[i]];
                        state.SetHeaderText(players[i].Name);
                        host.UpdateMemberState(players[i], state);
                    }
                }
            }
        }

        public void PauseGame()
        {
            paused = true;
        }

        public void ResumeGame()
        {
            paused = false;
        }

        public void NewGame()
        {
            Hackbox.State state = stateDisconnected.State;
            for (int i = 0; i < players.Length; i++)
            {
                state.SetHeaderText(players[i].Name);
                host.UpdateMemberState(players[i], state);
            }
            host.Disconnect();
            SceneManager.LoadScene("Game");
        }
    }
}
