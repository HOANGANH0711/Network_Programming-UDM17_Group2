#  Multiplayer Caro Game (Gomoku) 
**Project Code:** UDM_17
**Course:** Network Programming

---

## 👥 Team Members & Roles

To ensure efficient collaboration and maximize our 5-member team's productivity, we have adopted a structured software development lifecycle approach:

1. **Lê Hoàng Anh** - *Product Owner / Project Manager:*  
   Manages the project timeline, coordinates team activities, drafts project proposals, defines business requirements, manages the GitHub repository, and supervises overall project progress.

2. **[Member 2 Name]** - *System Architect / Backend Developer:*  
   Designs the Client-Server architecture, implements TCP/Socket communication, manages matchmaking logic, handles multithreading, and synchronizes data between clients and server.

3. **[Member 3 Name]** - *Game Logic Developer:*  
   Implements the Gomoku game algorithms including validating player moves, checking win/loss conditions (5 consecutive pieces), handling turn-based gameplay logic, and integrating game rules with the server.

4. **Ho Nguyen Dang Khoa** - *UI/UX Designer & Frontend Developer:*  
   Designs UI mockups using Figma and develops the Windows Forms graphical user interface (GUI), including the game board, player interface, notifications, and countdown timer system.

5. **Trần Thị Ánh Nguyệt** - *QA Engineer & Technical Writer:*  
   Conducts Stress Testing and Performance Testing, prepares test scripts and bug reports, compiles project documentation, collects testing evidence, creates presentation slides, and records the final demo video.

---

## 📁 Repository Structure
Following the strict guidelines provided by the lecturer:

- 📂 `Code/`: Contains all source code (Server, Client, and GUI scripts).
- 📂 `DOCX/`: Contains project proposals, business requirement documents, and system design specifications.
- 📂 `Extra/`: Contains UI mockup images, visual proofs of Stress/Performance tests, and demo videos.
- 📂 `PPTX/`: Contains presentation slides for the final evaluation.

---

##  Project Scope & Requirements

### Functional Requirements (FR)
- **Matchmaking:** Clients connect to the Server via IP and Port. The Server automatically pairs two connected clients into a game session.
- **Game Mechanics:** Turn-based Gomoku gameplay (X vs. O). The system automatically detects win/loss conditions (5 consecutive pieces horizontally, vertically, or diagonally).
- **Time Constraint:** A strict countdown timer (e.g., 15 seconds) is enforced for each turn. If a player fails to make a move within the time limit, they automatically lose the match.

### Non-Functional Requirements (NFR)
- **Platform:** The application must be a standalone GUI application running on Windows OS (No Web Applications allowed).
- **Performance:** The Server must be highly concurrent and capable of handling multiple simultaneous data transmissions without crashing.
- **Proof of Testing:** Comprehensive Stress and Performance tests must be conducted, with solid visual evidence provided in the repository.

---

##  Technology Stack
- **Programming Language:** C#
- **Network Protocol:** TCP/IP (Socket Programming)
- **GUI Framework:** Tkinter / PyQt

---

##  Project Progress & Milestones
*This checklist is updated at the End Of Week (EOW) to track continuous progress and prevent project failure due to inactivity.*

- [x] **Week 1:** Initialize repository structure, assign team roles, and establish the README documentation.
- [ ] **Week 2:** Finalize Project Proposal (`DOCX`), design initial UI Mockups (`Extra`), and define the data payload structure.
- [ ] **Week 3:** Implement core Socket Server/Client communication and basic GUI layout (`Code`).
- [ ] **Week 4:** Integrate game algorithms and the countdown timer logic.
- [ ] **Week 5:** Execute automated Stress Tests, document the results (`Extra`), and perform bug fixing.
- [ ] **Week 6:** Finalize presentation slides (`PPTX`), record the final Demo Video, and prepare for the defense.
