import classNames from 'classnames';
import React, { useContext, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Redirect, StaticContext } from 'react-router';
import { Route, RouteComponentProps, Switch, useHistory, withRouter } from 'react-router-dom';

import AppBar from '@material-ui/core/AppBar';
import Button from '@material-ui/core/Button';
import CircularProgress from '@material-ui/core/CircularProgress';
import Grid from '@material-ui/core/Grid';
import IconButton from '@material-ui/core/IconButton';
import { makeStyles } from '@material-ui/core/styles';
import Toolbar from '@material-ui/core/Toolbar';
import Typography from '@material-ui/core/Typography';
import useMediaQuery from '@material-ui/core/useMediaQuery';
import ArrowBackRoundedIcon from '@material-ui/icons/ArrowBackRounded';
import MenuIcon from '@material-ui/icons/Menu';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faSignOutAlt } from '@fortawesome/free-solid-svg-icons'

import Home from '../pages/home';
import Jobs from '../pages/jobs';
import { Login } from '../pages/login';
import Logs from '../pages/logs';
import MediaServer from '../pages/mediaServer';
import { MoviesGeneral, MoviesList, MoviesGraphs, MoviesLoader } from '../pages/movies';
import { ShowsGeneral, ShowsGraphs, ShowsList, ShowsLoader } from '../pages/shows';
import NotFound from '../pages/notFound';
import { GeneralSettings, MovieSettings, ShowSettings } from '../pages/settings';
import Menu from '../shared/components/menu';
import PrivateRoute from '../shared/components/privateRoute';
import UpdateProvider from '../shared/providers/UpdateProvider';
import { logout, userLoggedIn$ } from '../shared/services/AccountService';
import theme from '../styles/theme';
import { SettingsContext } from '../shared/context/settings';
import { WizardContainer } from '../pages/wizard';

const useStyles = makeStyles((theme) => ({
  root: {
    display: "flex",
    height: "100vh",
  },
  appBar: {
    zIndex: theme.zIndex.drawer + 1,
    transition: theme.transitions.create(["width", "margin"], {
      easing: theme.transitions.easing.sharp,
      duration: theme.transitions.duration.leavingScreen,
    }),
  },
  appBar__closed: {
    display: "none",
  },
  menuButton: {
    marginRight: theme.spacing(3),
    [theme.breakpoints.up("sm")]: {
      marginRight: theme.spacing(2),
    },
  },
  content: {
    padding: theme.spacing(3),
    marginTop: 48,
    width: "100%",
    zIndex: 1,
  },
  header__buttons: {
    width: 250,
  },
  button__loading: {
    color: "#d3d3d3",
  },
  logout__button: {
    height: 36,
    "&:hover": {
      backgroundColor: "transparent"
    }
  },
  toolbar__root: {
    backgroundColor: theme.palette.grey[800]
  }
}));

const LoggedIn = () => {
  const classes = useStyles();
  const { t } = useTranslation();
  const history = useHistory();
  const { settings } = useContext(SettingsContext);
  const [openMenu, setOpenMenu] = useState(false);
  const [openHeader, setOpenHeader] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const small = useMediaQuery(theme.breakpoints.down("md"));

  useEffect(() => {
    setOpenMenu(!small);
  }, [small]);

  useEffect(() => {
    setOpenMenu(isAuthenticated);
    setOpenHeader(isAuthenticated);
  }, [isAuthenticated])

  useEffect(() => {
    console.log(settings.wizardFinished);
    if (!settings.wizardFinished) {
      history.replace('/wizard');
    }
  }, [history, settings.wizardFinished])

  useEffect(() => {
    userLoggedIn$.subscribe((value: boolean) => {
      setIsAuthenticated(value);
    });
    return () => {
      userLoggedIn$.unsubscribe();
    };
  }, []);

  const handleDrawerToggle = () => {
    setOpenMenu(!openMenu);
  };

  const logoutUser = () => {
    setIsLoading(true);
    logout().finally(() => {
      history.push("/login", { referer: { pathname: "" } });
      setIsLoading(false);
    });
  };

  return (
    <div className={classes.root}>
      <UpdateProvider>
        {
          settings.wizardFinished && (
            <>
              <AppBar
                position="fixed"
                className={classNames(classes.appBar, {
                  [classes.appBar__closed]: !openHeader,
                })}
              >
                <Toolbar
                  classes={{
                    root: classes.toolbar__root
                  }}>
                  <Grid
                    container
                    direction="row"
                    alignItems="center"
                    justify="space-between"
                  >
                    <Grid
                      item
                      className={classes.header__buttons}
                      container
                      direction="row"
                      alignItems="center"
                    >
                      <IconButton
                        color="primary"
                        onClick={handleDrawerToggle}
                        edge="start"
                        className={classNames(classes.menuButton)}
                      >
                        {openMenu ? <ArrowBackRoundedIcon /> : <MenuIcon />}
                      </IconButton>
                      <Typography variant="h6" noWrap color="primary">
                        EmbyStat
                  </Typography>
                    </Grid>
                    <Grid item>
                      <Button
                        onClick={logoutUser}
                        variant="text"
                        color="primary"
                        disabled={isLoading}
                        className={classes.logout__button}
                      >
                        {isLoading ? (
                          <CircularProgress
                            size={16}
                            className={classes.button__loading}
                          />
                        ) : (
                            <span>
                              <FontAwesomeIcon icon={faSignOutAlt} className="m-r-8" />
                              {t("LOGIN.LOGOUT")}
                            </span>
                          )}
                      </Button>
                    </Grid>
                  </Grid>
                </Toolbar>
              </AppBar>
              <Menu open={openMenu} setOpen={setOpenMenu} />
            </>
          )
        }

        <main className={classes.content}>
          <Switch>
            <PrivateRoute path="/" exact>
              <Home />
            </PrivateRoute>
            <PrivateRoute path={["/movies/general", "/movies"]} exact>
              <MoviesLoader Component={MoviesGeneral} />
            </PrivateRoute>
            <PrivateRoute path="/movies/graphs" exact>
              <MoviesLoader Component={MoviesGraphs} />
            </PrivateRoute>
            <PrivateRoute path="/movies/list" exact>
              <MoviesLoader Component={MoviesList} />
            </PrivateRoute>
            <PrivateRoute path={["/shows/general", "/shows"]} exact>
              <ShowsLoader Component={ShowsGeneral} />
            </PrivateRoute>
            <PrivateRoute path="/shows/graphs" exact>
              <ShowsLoader Component={ShowsGraphs} />
            </PrivateRoute>
            <PrivateRoute path="/shows/list" exact>
              <ShowsLoader Component={ShowsList} />
            </PrivateRoute>
            <PrivateRoute path="/mediaserver" exact>
              <MediaServer />
            </PrivateRoute>
            <PrivateRoute path="/logs" exact>
              <Logs />
            </PrivateRoute>
            <PrivateRoute path="/jobs" exact>
              <Jobs />
            </PrivateRoute>
            <PrivateRoute path="/settings/general" exact>
              <GeneralSettings />
            </PrivateRoute>
            <PrivateRoute path="/settings/movie" exact>
              <MovieSettings />
            </PrivateRoute>
            <PrivateRoute path="/settings/show" exact>
              <ShowSettings />
            </PrivateRoute>
            <Route path="/login">
              {
                isAuthenticated
                  ? <Redirect to="/" />
                  : <Login />
              }
            </Route>
            <Route path="/wizard">
              <WizardContainer />
            </Route>
            <Route path="*">
              <NotFound />
            </Route>
          </Switch>
        </main>
      </UpdateProvider>
    </div>
  );
};

export default LoggedIn;
