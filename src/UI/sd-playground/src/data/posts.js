const posts = [
  {
    id: 1,
    author: "John Smith",
    timestamp: "2025-08-25T10:30:00Z",
    content: "Excited for week 7! Who you got in the big game?",
    likes: 5,
    dislikes: 0,
    userReaction: null,
    replies: [
      {
        id: 101,
        author: "Mike Brown",
        timestamp: "2025-08-25T11:00:00Z",
        content: "I'm taking Ohio State, easy cover!",
        likes: 2,
        dislikes: 0,
        userReaction: null,
        replies: []
      },
      {
        id: 102,
        author: "Sarah Johnson",
        timestamp: "2025-08-25T11:15:00Z",
        content: "Gotta root for the underdog, PSU all the way!",
        likes: 1,
        dislikes: 1,
        userReaction: null,
        replies: []
      }
    ]
  },
  {
    id: 2,
    author: "Chris Wilson",
    timestamp: "2025-08-25T12:00:00Z",
    content: "Anyone else worried about Alabama this year?",
    likes: 3,
    dislikes: 2,
    userReaction: null,
    replies: []
  }
];
export default posts;
