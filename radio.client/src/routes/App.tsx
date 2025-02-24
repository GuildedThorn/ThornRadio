import { useEffect, useState, useRef } from 'react';
import { Play, Pause, Volume2, VolumeX, RefreshCw } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Slider } from '@/components/ui/slider';
import { HttpTransportType, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const ICECAST_URL = 'https://radio.guildedthorn.com/stream';
const API_URL = process.env.NODE_ENV === "production"
    ? "https://yourdomain.com/chathub"
    : "http://localhost:5130/chathub";


function App() {
    const [playing, setPlaying] = useState(false);
    const [volume, setVolume] = useState(0.8);
    const [muted, setMuted] = useState(false);
    const [metadata, setMetadata] = useState({ title: 'Loading...', artist: 'Loading...' });
    const audioRef = useRef<HTMLAudioElement | null>(null);
    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState<any[]>([]);
    const [message, setMessage] = useState('');
    const [connecting, setConnecting] = useState(true);
    const connectionRef = useRef<ReturnType<typeof HubConnectionBuilder.prototype.build>>();
    const [, setOnlineUsers] = useState<string[]>([]);

    // Audio playback and metadata fetch
    useEffect(() => {
        if (!audioRef.current) {
            audioRef.current = new Audio(ICECAST_URL);
            audioRef.current.preload = 'auto';
        }

        const audio = audioRef.current;

        const fetchMetadata = async () => {
            try {
                const response = await fetch('/api/User/metadata');
                const data = await response.json();
                setMetadata({
                    title: data.title || 'Unknown Title',
                    artist: data.artist || 'Unknown Artist'
                });
            } catch (error) {
                console.error('Metadata fetch failed:', error);
            }
        };

        let metadataInterval: NodeJS.Timeout | undefined;

        audio.addEventListener('playing', () => setLoading(false));
        audio.addEventListener('waiting', () => setLoading(true));

        if (playing) {
            metadataInterval = setInterval(fetchMetadata, 5000);
            fetchMetadata();
            audio.play().catch(console.error);
        } else {
            audio.pause();
        }

        return () => {
            clearInterval(metadataInterval);
            audio.pause();
        };
    }, [playing]);

    // SignalR connection setup
    useEffect(() => {
        const createConnection = () => {
            return  new HubConnectionBuilder()
                .withUrl(API_URL, {
                    transport: HttpTransportType.WebSockets,  // Prefer WebSockets over LongPolling
                    withCredentials: true, // Ensures cookies are sent
                })
                .configureLogging(LogLevel.Information)
                .build();

        };

        const startConnection = async () => {
            if (connectionRef.current) return; // Avoid duplicate connections

            const conn = createConnection();
            connectionRef.current = conn;

            conn.onclose(async () => {
                console.warn("Connection lost. Retrying in 5 seconds...");
                setConnecting(true);
                setTimeout(startConnection, 5000); // Retry after delay
            });

            try {
                await conn.start();
                console.log("Connected to SignalR");
                setConnecting(false);

                conn.on("ReceiveMessage", (user, message, timestamp) => {
                    console.log('Received message:', user, message, timestamp); // Debug log
                    setMessages(prev => [...prev, {
                        user,
                        message,
                        timestamp: new Date(timestamp).toLocaleTimeString()
                    }]);
                });

                conn.on("UserConnected", (connectionId) => {
                    setOnlineUsers(prev => [...prev, connectionId]);
                });

                conn.on("UserDisconnected", (connectionId) => {
                    setOnlineUsers(prev => prev.filter(id => id !== connectionId));
                });

            } catch (err) {
                console.error("Connection failed:", err);
                setTimeout(startConnection, 5000);
            }
        };

        startConnection().then(() => { return() => {
            connectionRef.current?.stop();
        }});
    }, []);

    // Updated sendMessage function with a single argument (message object)
    const sendMessage = async () => {
        if (!connectionRef.current || connectionRef.current.state !== 'Connected') {
            console.error('Connection not ready');
            return;
        }

        try {
            // Send the message object to the backend
            const messageObject = {
                user: 'GuildedThorn',
                content: message, // 'message' refers to your input text
                timestamp: new Date().toISOString()
            };
            console.log('Sending message:', messageObject); // Debug log
            await connectionRef.current.invoke('SendMessage', messageObject);
            setMessage(''); // Clear input after sending
        } catch (error) {
            console.error('Send failed:', error);
        }
    };


    // Toggle playback
    const togglePlayback = () => {
        setPlaying(prev => !prev);
    };

    // Mute/unmute
    const toggleMute = () => {
        if (audioRef.current) {
            audioRef.current.muted = !muted;
            setMuted(!muted);
        }
    };

    // Handle volume change
    const handleVolumeChange = (value: number[]) => {
        if (audioRef.current) {
            const newVolume = value[0];
            audioRef.current.volume = newVolume;
            setVolume(newVolume);
            if (newVolume > 0 && muted) {
                setMuted(false);
                audioRef.current.muted = false;
            }
        }
    };


    return (
        <div className="flex flex-col md:flex-row space-y-6 md:space-y-0 md:space-x-8">
            {/* Player */}
            <Card className="w-full md:w-96">
                <CardContent className="p-6">
                    <div className="space-y-4">
                        <div className="text-center space-y-1">
                            <h2 className="font-semibold text-lg truncate">{metadata.title}</h2>
                            <p className="text-sm text-gray-500 truncate">{metadata.artist}</p>
                        </div>

                        {loading && (
                            <div className="flex justify-center">
                                <RefreshCw className="animate-spin" size={24} />
                            </div>
                        )}

                        <div className="flex items-center justify-center space-x-4">
                            <Button
                                variant="ghost"
                                size="icon"
                                onClick={toggleMute}
                                className="hover:bg-gray-100"
                            >
                                {muted ? <VolumeX size={20} /> : <Volume2 size={20} />}
                            </Button>

                            <Button
                                variant="default"
                                size="lg"
                                onClick={togglePlayback}
                                className="w-16 h-16 rounded-full"
                            >
                                {playing ? <Pause size={32} /> : <Play size={32} />}
                            </Button>

                            <div className="w-24">
                                <Slider
                                    value={[volume]}
                                    max={1}
                                    step={0.01}
                                    onValueChange={handleVolumeChange}
                                />
                            </div>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Chat Box */}
            <div className="w-full md:w-80">
                <div className="border p-4 rounded-lg h-96 flex flex-col space-y-4 overflow-auto">
                    <div className="space-y-2 flex-1">
                        {messages.length === 0 ? (
                            <div>No messages yet</div>
                        ) : (
                            messages.map((msg, index) => (
                                <div key={index} className="border-b py-2">
                                    <strong>{msg.user}:</strong> {msg.message}
                                </div>
                            ))
                        )}
                    </div>

                    {connecting && (
                        <div className="text-center text-gray-500">
                            Connecting to chat...
                        </div>
                    )}

                    <div className="flex items-center space-x-2">
                        <input
                            type="text"
                            value={message}
                            onChange={(e) => setMessage(e.target.value)}
                            placeholder="Type a message"
                            className="flex-1 p-2 border rounded"
                        />
                        <button
                            onClick={sendMessage}
                            className="p-2 bg-blue-500 text-white rounded"
                        >
                            Send
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default App;
