import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import SettingsPage from "./SettingsPage";
import apiWrapper from "../../api/apiWrapper";

// Only the account-deletion flow is covered here (SettingsPage's other sections
// are simple display/edit fields). We mock every collaborator the delete handler
// touches so we can assert the success/failure branches in isolation.

jest.mock("../../api/apiWrapper", () => ({
  Users: {
    getCurrentUser: jest.fn(),
    getNotificationPreferences: jest.fn(),
    deleteAccount: jest.fn(),
  },
  Auth: {
    clearToken: jest.fn(),
  },
}));

const mockNavigate = jest.fn();
jest.mock("react-router-dom", () => ({
  ...jest.requireActual("react-router-dom"),
  useNavigate: () => mockNavigate,
}));

const mockSignOut = jest.fn();
jest.mock("firebase/auth", () => ({
  getAuth: jest.fn(() => ({})),
  signOut: (...args) => mockSignOut(...args),
}));

const mockToastSuccess = jest.fn();
jest.mock("react-hot-toast", () => ({
  toast: { success: (...args) => mockToastSuccess(...args) },
}));

jest.mock("../../contexts/ThemeContext", () => ({
  useTheme: () => ({ theme: "dark", toggleTheme: jest.fn() }),
}));

jest.mock("../../contexts/UserContext", () => ({
  useUserDto: () => ({ refreshUserDto: jest.fn() }),
}));

// BadgesPanel pulls its own data; irrelevant to these tests.
jest.mock("../../components/badges/BadgesPanel.tsx", () => () => null);

const mockUser = {
  email: "real@person.com",
  username: "randall",
  displayName: "Randall",
  timezone: "America/New_York",
};

const allPrefsOn = {
  pickResultEnabled: true,
  pickDeadlineReminderEnabled: true,
  contestStartReminderEnabled: true,
  leagueInviteEnabled: true,
  membershipEnabled: true,
  matchupPreviewEnabled: true,
  scheduleChangeEnabled: true,
  oddsChangedEnabled: true,
};

function renderSettings() {
  return render(
    <MemoryRouter>
      <SettingsPage />
    </MemoryRouter>
  );
}

async function openDeleteConfirm() {
  await userEvent.click(await screen.findByRole("button", { name: /^Delete/i }));
  await userEvent.click(
    await screen.findByRole("button", { name: /yes, delete my account/i })
  );
}

describe("SettingsPage — account deletion", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    apiWrapper.Users.getCurrentUser.mockResolvedValue({ data: mockUser });
    apiWrapper.Users.getNotificationPreferences.mockResolvedValue({ data: allPrefsOn });
  });

  it("on success: clears token, signs out, toasts, and navigates home", async () => {
    apiWrapper.Users.deleteAccount.mockResolvedValue({});
    apiWrapper.Auth.clearToken.mockResolvedValue({});
    mockSignOut.mockResolvedValue(undefined);

    renderSettings();
    await openDeleteConfirm();

    await waitFor(() =>
      expect(apiWrapper.Users.deleteAccount).toHaveBeenCalledTimes(1)
    );
    expect(apiWrapper.Auth.clearToken).toHaveBeenCalledTimes(1);
    expect(mockSignOut).toHaveBeenCalledTimes(1);
    expect(mockToastSuccess).toHaveBeenCalled();
    expect(mockNavigate).toHaveBeenCalledWith("/");
  });

  it("on failure: shows error, stays put, and skips cleanup + navigation", async () => {
    apiWrapper.Users.deleteAccount.mockRejectedValue(new Error("boom"));

    renderSettings();
    await openDeleteConfirm();

    expect(
      await screen.findByText("We could not delete your account. Please try again.")
    ).toBeInTheDocument();

    expect(apiWrapper.Auth.clearToken).not.toHaveBeenCalled();
    expect(mockSignOut).not.toHaveBeenCalled();
    expect(mockNavigate).not.toHaveBeenCalled();

    // deleting state cleared → user can retry.
    expect(
      screen.getByRole("button", { name: /yes, delete my account/i })
    ).not.toBeDisabled();
  });
});
