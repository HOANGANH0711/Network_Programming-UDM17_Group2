#  Multiplayer Caro Game (Gomoku) 
**Project Code:** UDM_17
**Course:** Network Programming

---

## 👥 Team Members & Roles
To ensure efficient collaboration and maximize our 6-member team's productivity, we have adopted a structured software development lifecycle approach:

1. **Quan** - *Product Owner / Project Manager:* Manages project timeline, drafts proposals, defines business requirements, and delegates tasks.
2. **[Member 2 Name]** - *System Architect / Backend Developer:* Designs the network architecture (Client-Server model) and handles core TCP/Socket communication.
3. ****LE HOANG ANH**** - *Game Logic Developer:* Implements the Gomoku algorithms (validating coordinates, checking 5-in-a-row win/loss conditions).
4. **[Member 4 Name]** - *UI/UX Designer & Frontend Developer:* Creates UI mockups via Figma and develops the graphical user interface (GUI) and countdown timer.
5. **[Member 5 Name]** - *QA Engineer:* Develops test scripts to conduct Stress Testing and Performance Testing as per the project requirements.
6. **[Member 6 Name]** - *Technical Writer:* Compiles documentation, gathers testing proofs, designs presentation slides, and records the final demo video.

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
